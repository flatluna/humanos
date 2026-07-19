# HumanOS Backend — Guía del Runtime de Aprendizaje Interactivo

> **Para un nuevo developer:** Este documento explica cómo funciona el backend de HumanOS cuando un alumno interactúa con un módulo de aprendizaje. Cubre la **arquitectura de agentes**, las **bases de datos**, el **flujo de estados**, y el principio rector de todo: el **Memory Paradox**.

---

## Tabla de contenidos

1. [El principio rector: Memory Paradox](#el-principio-rector-memory-paradox)
2. [Arquitectura general](#arquitectura-general)
3. [Las bases de datos principales](#las-bases-de-datos-principales)
4. [Los agentes del Runtime](#los-agentes-del-runtime)
5. [Flujo de una sesión de aprendizaje](#flujo-de-una-sesión-de-aprendizaje)
6. [StudentEvidence: la prueba del aprendizaje](#studentevidence-la-prueba-del-aprendizaje)
7. [El Tutor Agent (TutorAgent)](#el-tutor-agent-tutoragent)
8. [Stages y transiciones de estado](#stages-y-transiciones-de-estado)
9. [Assessment y validación](#assessment-y-validación)
10. [Persistencia y checkpoints](#persistencia-y-checkpoints)

---

## El principio rector: Memory Paradox

Toda decisión arquitectónica en el Runtime de HumanOS está gobernada por UN principio fundamental:

> **"La IA debe fortalecer la memoria, el conocimiento, el pensamiento y la autonomía humana, no sustituirlos."**
>
> — *The Memory Paradox* (Oakley et al., 2025)

### ¿Qué significa esto en código?

1. **Consumo NO es aprendizaje**: Ver un video, leer un texto, pasar un módulo — NINGUNO de estos es evidencia de aprendizaje.
   - Evidencia real = **StudentEvidence** (lo que el alumno PRODUCE).
   - Consumo = presentación (nunca crea una `StudentEvidence`).

2. **Recuperación ANTES de ayuda**: El alumno SIEMPRE intenta recordar o predecir SIN ayuda, ANTES de que se le muestre la respuesta, el ejemplo, la pista o que la IA intervenga.
   - Esto es `RecallRequired` + `PredictionRequired` en el estado máquina.
   - No existe "comodidad": la fricción productiva es parte del diseño pedagógico.

3. **"Saber dónde buscarlo" ≠ "Saberlo"**: Si un alumno consigue una respuesta correcta PORQUE la IA se la escribió, eso no es evidencia de capacidad — es evidencia de dependencia.
   - Esto se mide con `EvidenceAssistanceLevel` (Unaided → WithRetrievalCues → WithGuidedHints → WithAiAssistance).
   - Solo `Unaided` y `WithRetrievalCues` cuentan como evidencia fuerte.

---

## Arquitectura general

```
┌─────────────────────────────────────────────────────────────┐
│  Frontend (React/Vite)                                      │
│  - StudentApp: alumno responde preguntas                   │
│  - MemoryParadox.tsx: explica el concepto                 │
└────────────────────┬────────────────────────────────────────┘
                     │ HTTP API calls
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  Backend: HumanOS (C# / .NET 10)                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Microsoft Agent Framework Workflow (Runtime)       │   │
│  │ ┌───────────────────────────────────────────────┐   │   │
│  │ │ RuntimeStageExecutors (deterministic FSM)    │   │   │
│  │ │ - ModuleStarted                              │   │   │
│  │ │ - RecallRequired ──► [pausa] ─┐              │   │   │
│  │ │ - PredictionRequired ──► [pausa]              │   │   │
│  │ │ - ChapterTeaching / ChapterRecall / ...       │   │   │
│  │ │ - LearnerProduction ──► [pausa]               │   │   │
│  │ │ - Assessment (TutorAgent validates)          │   │   │
│  │ │ - Reflection ──► [pausa]                      │   │   │
│  │ │ - Completed / RequiresRevision (terminal)    │   │   │
│  │ └───────────────────────────────────────────────┘   │   │
│  │          ▲                                           │   │
│  │          │ orquestación                             │   │
│  │          ▼                                           │   │
│  │ ┌───────────────────────────────────────────────┐   │   │
│  │ │ TutorAgent (Harness-based, 1 solo agente)   │   │   │
│  │ │ - RecallSkill / PredictionSkill / ...        │   │   │
│  │ │ - Lee TutorKnowledgeBase via RAG             │   │   │
│  │ │ - Genera respuestas pedagógicas              │   │   │
│  │ └───────────────────────────────────────────────┘   │   │
│  │          ▲                                           │   │
│  │          │ consulta                                 │   │
│  │          ▼                                           │   │
│  │ ┌───────────────────────────────────────────────┐   │   │
│  │ │ CapabilityKnowledgeChunk (embeddings VECTOR) │   │   │
│  │ │ - RAG para el Tutor                          │   │   │
│  │ │ - Troceado del guion final de cada módulo    │   │   │
│  │ └───────────────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Servicios de dominio                               │   │
│  │ - EvidenceService: registra StudentEvidence       │   │
│  │ - RecallService: tracking de recuperación         │   │
│  │ - AssessmentService: evaluación manual            │   │
│  │ - PersonCapabilityService: progresión del alumno │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Studio (Agents de GENERACIÓN de contenido)        │   │
│  │ - CuradorAgent: organiza material crudo           │   │
│  │ - ArquitectoAgent: diseña blueprint               │   │
│  │ - InstructorAgent: escribe guiones                │   │
│  │ - MetricoAgent: verifica métricas                 │   │
│  │ - ExperienciaAgent: ensambla paquete final        │   │
│  │ (Studio ≠ Runtime, aunque Studio produce lo       │   │
│  │  que Runtime consume)                             │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                     │
                     ▼
          ┌──────────────────────┐
          │   Azure SQL Database │
          │   (tablas abajo)     │
          └──────────────────────┘
```

---

## Las bases de datos principales

### 1. **Tabla: `Evidence`**

Almacena **TODAS** las pruebas de aprendizaje que produce un alumno.

```sql
CREATE TABLE Evidence
(
    EvidenceId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PersonId UNIQUEIDENTIFIER NOT NULL,           -- alumno
    CapabilityId UNIQUEIDENTIFIER NOT NULL,       -- capacidad que practica
    PersonProjectId UNIQUEIDENTIFIER NULL,         -- proyecto (si aplica)
    Title NVARCHAR(400) NOT NULL,                  -- título de la evidencia
    Description NVARCHAR(MAX) NULL,               -- detalles
    EvidenceType NVARCHAR(100) NOT NULL,          -- tipo: "Text", "Image", "Code", "Video", etc.
    EvidenceUrl NVARCHAR(2000) NULL,              -- URL de almacenamiento (blob)
    ValidationStatus NVARCHAR(30) DEFAULT 'Pending', -- Pending / Accepted / Rejected
    AssistanceLevel INT DEFAULT 0,                 -- 0=Unaided, 1=WithRetrievalCues, 2=WithGuidedHints, 3=WithAiAssistance
    ValidationFeedback NVARCHAR(MAX) NULL,        -- comentarios del revisor
    ValidatedDate DATETIME2 NULL,
    SubmittedDate DATETIME2 DEFAULT SYSUTCDATETIME(),
    CreatedDate DATETIME2 DEFAULT SYSUTCDATETIME(),
    
    -- Restricciones:
    CONSTRAINT FK_Evidence_Person FOREIGN KEY (PersonId) REFERENCES Person(PersonId),
    CONSTRAINT FK_Evidence_Capability FOREIGN KEY (CapabilityId) REFERENCES Capability(CapabilityId),
    CONSTRAINT CK_Evidence_ValidationStatus CHECK (ValidationStatus IN ('Pending', 'Accepted', 'Rejected')),
    CONSTRAINT CK_Evidence_AssistanceLevel CHECK (AssistanceLevel BETWEEN 0 AND 3),
    CONSTRAINT CK_Evidence_ValidationState CHECK (
        (ValidationStatus = 'Pending' AND ValidatedDate IS NULL)
        OR
        (ValidationStatus IN ('Accepted', 'Rejected') AND ValidatedDate IS NOT NULL)
    )
);
```

**Claves:**
- Una `Evidence` es UNA prueba individual del alumno.
- El alumno puede tener MUCHAS `Evidence` para la misma `Capability` (iteración, práctica, retry).
- `AssistanceLevel` es crítico: determina si la evidencia cuenta como "aprendizaje real" o "dependencia de IA".

---

### 2. **Tabla: `CapabilityEvidence`**

Conecta una `Evidence` con la `Capability` a través de `PersonCapability`.

```sql
CREATE TABLE CapabilityEvidence
(
    CapabilityEvidenceId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PersonCapabilityId UNIQUEIDENTIFIER NOT NULL,  -- avance del alumno en la capacidad
    EvidenceId UNIQUEIDENTIFIER NOT NULL,          -- la prueba
    EvidenceType NVARCHAR(100) NOT NULL,           -- tipo (duplicado para búsqueda rápida)
    ContributionWeight DECIMAL(5,2) DEFAULT 1.0,   -- cuánto cuenta (0.0 = ignora, 1.0 = full)
    ValidationStatus NVARCHAR(30) DEFAULT 'Pending',
    ValidatedByPersonId UNIQUEIDENTIFIER NULL,     -- quién validó
    ValidatedDate DATETIME2 NULL,
    CreatedDate DATETIME2 DEFAULT SYSUTCDATETIME(),
    
    CONSTRAINT FK_CapabilityEvidence_PersonCapability FOREIGN KEY (PersonCapabilityId) REFERENCES PersonCapability(PersonCapabilityId),
    CONSTRAINT FK_CapabilityEvidence_Evidence FOREIGN KEY (EvidenceId) REFERENCES Evidence(EvidenceId),
    CONSTRAINT FK_CapabilityEvidence_Validator FOREIGN KEY (ValidatedByPersonId) REFERENCES Person(PersonId)
);
```

**Para qué sirve:**
- Vincula evidencia individual a la progresión del alumno en una capacidad.
- El `ContributionWeight` permite "pesar" diferentes evidencias (ej. un proyecto final = 2.0, un quick quiz = 0.5).

---

### 3. **Tabla: `RuntimeWorkflowCheckpoint`**

Persiste el estado del Workflow de Agent Framework durante sesiones largas.

```sql
CREATE TABLE RuntimeWorkflowCheckpoint
(
    RuntimeWorkflowCheckpointId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    SessionId NVARCHAR(255) NOT NULL,              -- ID de la sesión del alumno
    CheckpointId NVARCHAR(255) NOT NULL,           -- ID del checkpoint en el Workflow
    ParentCheckpointId NVARCHAR(255) NULL,         -- para cadenas de retry
    PayloadJson NVARCHAR(MAX) NOT NULL,            -- estado serializado (opaco)
    CreatedDate DATETIME2 DEFAULT SYSUTCDATETIME(),
    
    CONSTRAINT UQ_RuntimeWorkflowCheckpoint UNIQUE (SessionId, CheckpointId)
);
```

**Para qué sirve:**
- Permite que una sesión de aprendizaje se pause (ej. alumno responde una pregunta, se va, vuelve mañana).
- El `PayloadJson` contiene el estado completo del `RuntimeSession` + el historial de `StudentEvidence` capturada hasta ese punto.
- Sin esto, si el servidor se reinicia o el alumno cierra el navegador, todo se pierde.

---

### 4. **Tabla: `CapabilityModuleVerification`** (desde Studio, pero usado por Runtime)

Registra cómo el agente Métrico de Studio verificó cada módulo.

```sql
CREATE TABLE CapabilityModuleVerification
(
    CapabilityModuleVerificationId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CapabilityModuleId UNIQUEIDENTIFIER NOT NULL,  -- el módulo verificado
    TargetMetric NVARCHAR(50) NOT NULL,            -- métrica objetivo (Knowledge, Recall, Application, etc.)
    Status NVARCHAR(30) NOT NULL,                  -- Verified / NotVerified / Failed
    Evidence NVARCHAR(MAX) NOT NULL,               -- descripción de la evidencia encontrada en el guion
    EvidenceLocation NVARCHAR(MAX) NOT NULL,       -- dónde en el guion (línea/párrafo)
    Explanation NVARCHAR(MAX) NOT NULL,            -- por qué sí/no verifica esa métrica
    RecallStatus NVARCHAR(30) NOT NULL,            -- Missing / WithCues / WithoutCues
    RecallEvidence NVARCHAR(MAX) NOT NULL,         -- qué Recall se encontró
    RecallEvidenceLocation NVARCHAR(MAX) NOT NULL,
    RecallOccursBeforeInstruction BIT NOT NULL,    -- CRUCIAL: ¿recupera ANTES de instrucción?
    CreatedDate DATETIME2 DEFAULT SYSUTCDATETIME(),
    
    CONSTRAINT FK_CapabilityModuleVerification_Module FOREIGN KEY (CapabilityModuleId) 
        REFERENCES CapabilityModule(CapabilityModuleId)
);
```

**Para el Runtime:**
- El Runtime consulta esto para entender cuál es la **contrato pedagógico** (`RuntimePedagogicalContract`) del módulo.
- Sabe cuál es la métrica objetivo y qué evidencia necesita capturar del alumno.

---

### 5. **Tabla: `CapabilityModule`** (desde Studio, consumida por Runtime)

```sql
CREATE TABLE CapabilityModule
(
    CapabilityModuleId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CapabilityLevelId UNIQUEIDENTIFIER NOT NULL,   -- en qué nivel (Foundation, Exploration, Mastery)
    ModuleType NVARCHAR(50) NOT NULL,              -- Lectura / Video / Practica / SimuladorIA
    Title NVARCHAR(500) NOT NULL,
    Description NVARCHAR(MAX) NOT NULL,
    Script NVARCHAR(MAX) NOT NULL,                 -- guion completo del módulo
    
    -- Contrato pedagógico (desde Studio):
    TargetMetric NVARCHAR(50) NOT NULL,            -- Knowledge, Recall, Application, Confidence, Independence, Retention, Fluency
    RecallRequirement NVARCHAR(MAX) NOT NULL,      -- qué debe recuperar el alumno SIN ayuda
    LearnerProduction NVARCHAR(MAX) NOT NULL,      -- qué debe PRODUCIR el alumno (tarea concreta)
    SuccessCriteria NVARCHAR(MAX) NOT NULL,        -- JSON array de criterios (2-5 criterios)
    
    -- Chapters (desde Studio, 2026-07-16):
    HasChapters BIT DEFAULT 0,                     -- ¿tiene presentación fase por fase?
    Chapters NVARCHAR(MAX) NULL,                   -- JSON array de Capítulos
    
    IsActive BIT DEFAULT 1,
    CreatedDate DATETIME2 DEFAULT SYSUTCDATETIME(),
    UpdatedDate DATETIME2 DEFAULT SYSUTCDATETIME()
);
```

**Para el Runtime:**
- El Runtime carga esto cuando inicia una sesión.
- Extrae el `TargetMetric`, `RecallRequirement`, `LearnerProduction`, `SuccessCriteria` y `Chapters`.
- Estos definen TODAS las etapas por las que pasará el alumno (es el "contrato").

---

### 6. **Tabla: `RuntimeSession`** (definida en tipos, persistida vía checkpoint)

NO tiene tabla propia en SQL (todavía). Vive serializada en `RuntimeWorkflowCheckpoint.PayloadJson`.

En código (C#):
```csharp
public class RuntimeSession
{
    public Guid RuntimeSessionId { get; set; }
    public Guid PersonId { get; set; }
    public Guid CapabilityModuleId { get; set; }
    public RuntimeStage Stage { get; set; }  // estado actual: RecallRequired, PredictionRequired, etc.
    public RuntimePedagogicalContract Contract { get; set; }
    public List<StudentEvidence> CapturedEvidence { get; set; }  // evidencia recolectada
    public List<RuntimeStageTransition> History { get; set; }    // auditoría de transiciones
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
}
```

---

## Los agentes del Runtime

### Studio vs. Runtime: agentes diferentes

**Studio** (genera contenido, OFFLINE):
- CuradorAgent, ArquitectoAgent, InstructorAgent, MetricoAgent, ExperienciaAgent
- Corren UNA SOLA VEZ por capacidad.
- Producen: CapabilityModule con guion, contrato pedagógico, TutorKnowledgeBase.

**Runtime** (consume contenido, ONLINE, durante aprendizaje vivo):
- **TutorAgent** (Harness-based, único agente para todo el runtime de vida de la sesión)
- Skills pedagógicas: RecallSkill, PredictionSkill, ApplicationSkill, ConfidenceSkill, IndependenceSkill, AssessmentSkill, ReflectionSkill
- **NO hay multi-agente**: no hay handoff entre agentes por cada turno (sería latencia inasumible).

---

## Studio: Cómo se CREA el conocimiento (Pipeline de 5 agentes)

Human OS Studio es una **fábrica de contenido pedagógico** que transforma material crudo (PDFs, videos, notas) en módulos de aprendizaje verificados, listos para que el Runtime los use. El proceso es determinista, auditado y **refuerza el Memory Paradox en cada paso**.

### El principio: NUNCA crear cognitive offloading

> **REGLA DE ORO** (grabada en los prompts de los 3 agentes que generan contenido):
> **"Saber dónde buscarlo NO es saberlo."** El alumno SIEMPRE debe producir algo. Ningún agente debe hacer el trabajo cognitivo por él.

### Arquitectura del pipeline Studio

```
ENTRADA: Material crudo
    ├─ PDFs de investigación
    ├─ Transcripciones de video
    ├─ Links / notas del experto
    ├─ Objetivos de la capacidad
    └─ Contexto del dominio
    
        ↓
        
AGENTE 1: CURADOR
    ├─ Recibe material crudo del experto
    ├─ Organiza en "corpus curado": resumen general + chunks etiquetados
    ├─ NUNCA inventa hechos
    ├─ Extrae solo lo que el material respalda
    └─► Salida: CorpusModel (resumen + chunks etiquetados)
    
        ↓
        
AGENTE 1.5: GRAPH ARCHITECT (NUEVO EN PASO 2)
    ├─ Lee CorpusModel curado
    ├─ Identifica conceptos importantes (Variables, Arrays, Funciones, ...)
    ├─ Identifica skills importantes (Escribir bucles, Debuggear, ...)
    ├─ Crea nodos (LearningNodeType: Concept o Skill)
    ├─ Define relaciones (RelationshipType: Requires o BuildsOn)
    ├─ NUNCA inventa conceptos no respaldados por corpus
    ├─ Mantiene grafo pequeño y comprensible (max ~20 nodos por capability)
    ├─ Persiste en BD: CapabilityGraph + CapabilityGraphNodes + CapabilityGraphEdges
    └─► Salida: CapabilityGraph con Nodes + Edges
    
        ↓
        
AGENTE 2: ARQUITECTO
    ├─ Lee corpus curado
    ├─ Decide SCOPE (qué niveles, qué métricas)
    ├─ Diseña blueprint (estructura de módulos)
    ├─ Define contrato pedagógico POR MÓDULO
    │  ├─ TargetMetric (Knowledge, Recall, Application, etc.)
    │  ├─ RecallRequirement (qué recupera el alumno)
    │  ├─ LearnerProduction (qué crea el alumno)
    │  └─ SuccessCriteria (2-5 criterios observables)
    └─► Salida: Blueprint (esqueleto de módulos + contrato)
    
        ↓
        
GATE 1: REVISIÓN HUMANA DEL BLUEPRINT
    ├─ Humano revisa: ¿el scope tiene sentido?
    ├─ Humano revisa: ¿cada módulo tiene contrato claro?
    ├─ Humano revisa: ¿RecallRequirement y LearnerProduction son concretos?
    ├─ Aprobado → continúa
    └─ Rechazado → vuelve al Arquitecto
    
        ↓
        
AGENTE 3: INSTRUCTOR
    ├─ Lee corpus + blueprint aprobado
    ├─ Escribe guion REAL para cada módulo, uno a la vez
    ├─ Aplica 7 principios de neurociencia:
    │  ├─ P1: Prediction error (predice ANTES)
    │  ├─ P2: Two systems (deliberado + automático)
    │  ├─ P3: Desirable difficulties (fricción productiva)
    │  ├─ P4: Encoding specificity (contexto real)
    │  ├─ P5: Schema (conecta con lo conocido)
    │  ├─ P6: Consolidation (repaso espaciado)
    │  └─ P7: Anti-offloading (alumno SIEMPRE produce)
    ├─ Estructura del guion:
    │  ├─ RECALL FIRST: alumno recupera SIN ayuda ANTES
    │  ├─ PREDICTION: alumno predice cómo se aplicará
    │  ├─ CHAPTERS: presentación en fases (cada capítulo ~5 min)
    │  │  ├─ Chapter teaching (contenido)
    │  │  ├─ Chapter recall (recuperación mini)
    │  │  ├─ Chapter prediction (para chapter "primary")
    │  │  └─ Chapter mini-practice (ejercicio rápido)
    │  ├─ LEARNER PRODUCTION: alumno CREA el artefacto
    │  └─ REFLECTION: compara predicción vs. resultado
    ├─ Implementa el contrato pedagógico explícitamente
    └─► Salida: ModuleScript (guion + estructura + validación)
    
        ↓
        
AGENTE 4: MÉTRICO
    ├─ Lee guion del Instructor
    ├─ Verifica: ¿REALMENTE se logra TargetMetric?
    ├─ Busca EVIDENCIA concreta en el guion:
    │  ├─ Para Recall: ¿recupera sin pistas? ¿ANTES de instrucción?
    │  ├─ Para Application: ¿usa lo aprendido en caso real?
    │  ├─ Para Confidence: ¿compara confianza predicha vs. desempeño real?
    │  ├─ Para Independence: ¿sin pasos, ejemplos, pistas, IA?
    │  ├─ Para Retention: ¿se programa repaso espaciado?
    │  └─ Para cada criterio de éxito: ¿cómo se verifica?
    ├─ Si falta evidencia:
    │  └─ Status = NotVerified (el módulo necesita revisión)
    ├─ Si evidencia es sólida:
    │  └─ Status = Verified (módulo listo)
    └─► Salida: MetricVerification (evidencia + explicación + Status)
    
        ↓
        
UMBRAL DEL 85% (nuevo en Paso 7):
    ├─ Si 85%+ de módulos están Verified
    │  └─► continúa a Experiencia
    ├─ Si <85%
    │  ├─ Módulos Verified sí se publican
    │  └─ Módulos NotVerified quedan pendientes (RequiresRevision)
    
        ↓
        
AGENTE 5: EXPERIENCIA
    ├─ Recibe TODOS los módulos (Verified + RequiresRevision)
    ├─ Ensambla el CapabilityPackage final
    ├─ Genera TutorKnowledgeBase (RAG)
    │  ├─ Consolida todos los guiones
    │  ├─ Trocea en chunks para RAG
    │  ├─ Embebe con text-embedding-ada-002 (Azure OpenAI)
    │  └─ Almacena embeddings en Azure SQL (columna VECTOR)
    ├─ Redacta resumen pedagógico de la capacidad
    └─► Salida: CapabilityPackage + TutorKnowledgeBase (embeddings)
    
        ↓
        
GATE 2: REVISIÓN HUMANA FINAL
    ├─ Humano revisa: ¿el paquete completo tiene coherencia?
    ├─ Humano revisa: ¿TutorKnowledgeBase tiene calidad suficiente?
    ├─ Humano revisa: ¿los módulos RequiresRevision son aceptables?
    ├─ Aprobado → PUBLICA
    └─ Rechazado → vuelve al Instructor/Métrico
    
        ↓
        
SALIDA: Capability PUBLICADA
    ├─ CapabilityModule (1 por módulo del blueprint)
    ├─ CapabilityLevel (Foundation / Exploration / Mastery)
    ├─ CapabilityModuleVerification (auditoría de validación)
    ├─ CapabilityKnowledgeChunk (embeddings para RAG)
    └─ TutorKnowledgeBase (referencia consolidada)
    
        ↓
        
Runtime carga esto → alumno interactúa → TutorAgent consulta via RAG
```

---

### Detalle de cada agente: qué hace EXACTAMENTE

#### **AGENTE 1: CURADOR**

**Qué recibe:**
- PDF research paper (20 páginas)
- Video transcription (45 minutos)
- Link a blog post
- Notas del experto (3 páginas)

**Qué HACE:**
1. Lee y extrae SOLO lo que está en las fuentes (NUNCA inventa).
2. Agrupa por tema/concepto.
3. Etiqueta cada chunk: [DEFINITION], [EXAMPLE], [COUNTEREXAMPLE], [APPLICATION], [WARNING].
4. Crea resumen general (3-5 párrafos).

**Qué devuelve:**
```json
{
  "capability": "Database Optimization",
  "overallSummary": "...",
  "curatedChunks": [
    {
      "id": "chunk_001",
      "tag": "DEFINITION",
      "content": "Indexing is a database structure...",
      "source": "research_paper.pdf, page 5"
    },
    {
      "id": "chunk_002",
      "tag": "EXAMPLE",
      "content": "Example: CREATE INDEX idx_user_email...",
      "source": "video_transcript.txt, 12:34"
    },
    ...
  ]
}
```

**Memory Paradox aquí:**
- El Curador NUNCA edita/interpreta. Solo ordena lo que existe.
- Si algo no está en las fuentes, se nota como [NO EVIDENCE IN CORPUS].

---

#### **AGENTE 1.5: GRAPH ARCHITECT** (Paso 2)

**Qué recibe:**
- CorpusModel (resumen + chunks etiquetados del Curador)
- Objetivo del experto (nombre de la capability, dominio)

**Qué HACE:**

1. **Analiza el corpus y extrae conceptos/skills**.
   - Lee cada chunk etiquetado ([DEFINITION], [EXAMPLE], [APPLICATION], etc.)
   - Identifica "atoms" de aprendizaje (Variables, Functions, Arrays, Loops, Error Handling, etc.)
   - Pregunta: ¿está este concepto respaldado por al menos 2 chunks?
   - Si sí → es un nodo candidato.
   - Si no → se desestima.

2. **Clasifica cada nodo como Concept o Skill**.
   - Concept: teórico, se explica (Variables, Tipos de Datos, Paradigmas, ...)
   - Skill: práctico, se hace (Escribir un bucle, Depurar, Optimizar índices, ...)

3. **Identifica relaciones entre nodos**.
   - Requires: nodo A REQUIERE conocimiento de B antes (Bucles Requires Variables)
   - BuildsOn: nodo A se CONSTRUYE SOBRE B (Recursión BuildsOn Funciones)

4. **Construye el grafo**.
   - Nodes: Lista de LearningNodeType (Concept o Skill)
   - Edges: Lista de RelationshipType (Requires o BuildsOn)
   - SortOrder: orden pedagógico (1, 2, 3, ...)

5. **Valida el grafo**.
   - ¿Ciclos? (no permitidos en DAG)
   - ¿Duplicados? (no)
   - ¿Orfandades? (nodos sin relaciones, are OK si son "entry points")
   - ¿Tamaño? (máximo ~20 nodos por capability para mantenerlo comprensible)

**Qué devuelve:**
```json
{
  "capabilityGraphId": "uuid",
  "capabilityId": "uuid",
  "name": "Programming Fundamentals Graph",
  "description": "Grafo de dependencias para programación básica",
  "nodes": [
    {
      "id": "uuid_var",
      "name": "Variables",
      "description": "Contenedores para almacenar datos",
      "nodeType": "Concept",
      "sortOrder": 1
    },
    {
      "id": "uuid_loops",
      "name": "Loops",
      "description": "Repetición de bloques de código",
      "nodeType": "Skill",
      "sortOrder": 3
    },
    ...
  ],
  "edges": [
    {
      "sourceNodeId": "uuid_loops",
      "targetNodeId": "uuid_var",
      "relationshipType": "Requires"
    },
    {
      "sourceNodeId": "uuid_recursion",
      "targetNodeId": "uuid_functions",
      "relationshipType": "BuildsOn"
    },
    ...
  ]
}
```

**Memory Paradox aquí:**
- GraphArchitect NUNCA inventa nodos.
- Si un concepto no está respaldado por el corpus (al menos 2 chunks), se descarta.
- El grafo es una **representación objetiva** de lo que el Curador encontró, no una innovación del agente.

**Ejemplo de uso (código C# en el backend):**

```csharp
// En un controller o servicio Studio:
var curatedChunks = new List<(string Tag, string Content, string? Source)>
{
    ("DEFINITION", "A variable is a named container for storing data values...", "Chapter 2"),
    ("EXAMPLE", "int age = 25; // age is a variable holding the integer 25", "Chapter 2"),
    ("DEFINITION", "A loop repeats a block of code multiple times...", "Chapter 3"),
    ("EXAMPLE", "for (int i = 0; i < 10; i++) { ... }", "Chapter 3"),
    ("APPLICATION", "Write a loop that prints each variable's value", "Exercise 3.1"),
    // ... más chunks
};

var orchestrator = new GraphArchitectOrchestrator(
    graphAgent: new GraphArchitectAgent(),
    persistenceService: new GraphPersistenceService(context),
    context: context
);

var graphResponse = await orchestrator.ExecuteGraphBuildingAsync(
    capabilityId: capabilityId,
    capabilityName: "Programming Fundamentals",
    overallSummary: "Basic programming concepts from variables to loops",
    curatedChunks: curatedChunks,
    domainContext: "Computer Science 101",
    cancellationToken: cancellationToken
);

// graphResponse.Nodes contiene: [Variables, Types, Loops, Conditionals, ...]
// graphResponse.Edges contiene: [Loops→Variables (Requires), Conditionals→Variables (Requires), ...]
// GraphArchitectOrchestrator persiste automáticamente en BD
```

**Notas de implementación:**

- **Ubicación del código:** `/backend/HumanOS/Agents/Studio/GraphArchitect/` (3 archivos)
  - `GraphArchitectTypes.cs`: DTOs de solicitud/respuesta
  - `GraphArchitectAgent.cs`: Lógica de extracción y construcción de grafo
  - `GraphPersistenceService.cs`: Persistencia en BD (CapabilityGraphs, Nodes, Edges)
  - `GraphArchitectOrchestrator.cs`: Orquestación del flujo completo

- **Almacenamiento:** Grafo persistido en 3 tablas de Azure SQL:
  - `CapabilityGraphs`: raíz (1 por capability)
  - `CapabilityGraphNodes`: nodos (múltiples)
  - `CapabilityGraphEdges`: aristas (múltiples)

- **Validación:**
  - DAG (sin ciclos)
  - Sin auto-loops
  - Sin duplicados
  - Máximo ~30 nodos

- **Integración en pipeline:**
  - PASO 1: CuradorAgent produce chunks curados
  - PASO 2: GraphArchitectAgent produce grafo
  - PASO 3: (Future) ArquitectoAgent consume grafo para diseñar módulos
  - **Nota:** Por ahora, ArquitectoAgent NO consume grafo (es preparatorio para PASO 3)

---

#### **AGENTE 2: ARQUITECTO**

**Qué recibe:**
- Corpus curado (resumen + chunks)
- Objetivo del experto ("Teach database optimization from beginner to expert user")

**Qué DECIDE:**

1. **SCOPE (qué niveles, qué métricas)**
   - Cuántos niveles realmente necesita? (min 2, max 6 actualmente: Foundation, Exploration, Mastery)
   - Qué métricas? (Knowledge, Recall, Application, Confidence, Independence, Retention, Fluency)
   - Ej: "Foundation (Knowledge) → Exploration (Knowledge+Recall) → Mastery (Application+Independence)"

2. **POR CADA MÓDULO en cada nivel:**
   - `TargetMetric`: solo UNA (no lista).
   - `RecallRequirement`: qué debe recuperar el alumno ANTES de instrucción.
   - `LearnerProduction`: qué CREA el alumno (artefacto observable).
   - `SuccessCriteria`: 2-5 criterios (ej. "query runs in <100ms", "uses appropriate index", "no full table scans").

**Qué devuelve:**
```json
{
  "capability": "Database Optimization",
  "scope": "Foundation + Exploration + Mastery | Knowledge, Recall, Application, Independence",
  "modules": [
    {
      "level": "Foundation",
      "moduleIndex": 1,
      "title": "What is an Index?",
      "targetMetric": "Knowledge",
      "recallRequirement": "What's the difference between a full table scan and an indexed search?",
      "learnerProduction": "Explain in your own words: what does an index do?",
      "successCriteria": [
        "Describes index as data structure",
        "Mentions speed/lookup improvement",
        "Uses own example (not copy-paste)"
      ]
    },
    {
      "level": "Exploration",
      "moduleIndex": 2,
      "title": "Creating Indexes in Practice",
      "targetMetric": "Application",
      "recallRequirement": "When would you use an index? In what scenario?",
      "learnerProduction": "Write a CREATE INDEX statement for your own database schema",
      "successCriteria": [
        "Syntax is correct",
        "Index chosen is appropriate for the schema",
        "Column selection is justified in comments"
      ]
    },
    ...
  ]
}
```

**Memory Paradox aquí:**
- El Arquitecto NUNCA oversells (ej: no promete 7 módulos si el corpus solo respalda 3).
- Cada módulo tiene un propósito claro, medible.

---

#### **GATE 1: REVISIÓN HUMANA**

Humano (Subject Matter Expert o pedagogo):
- ✅ ¿El scope es realista?
- ✅ ¿Cada RecallRequirement es recuperable (alumno podría haberlo visto antes)?
- ✅ ¿Cada LearnerProduction es creación auténtica (no consumo)?
- ✅ ¿Los SuccessCriteria son observables? (NO "student understands", SÍ "student explains without AI help")

Si OK → Arquitecto blueprint aprobado, avanza.  
Si NO → vuelve al Arquitecto con feedback.

---

#### **AGENTE 3: INSTRUCTOR**

**Qué recibe:**
- Corpus curado
- Blueprint aprobado (módulos + contrato)

**Qué HACE (para cada módulo):**

Escribe guion estructurado que IMPLEMENTA el contrato:

```
GUION COMPLETO DEL MÓDULO
═════════════════════════════

MODULO: "Creating Indexes in Practice" (Exploration, Application)
CONTRATO:
  - TargetMetric: Application
  - RecallRequirement: "When would you use an index?"
  - LearnerProduction: "Write a CREATE INDEX statement"
  - SuccessCriteria: [syntax OK, appropriate for schema, justified]

ESTRUCTURA (con 7 principios de neurociencia):
─────────────────────────────────────────────

1. RECALL FIRST (P1 + P7 Anti-offloading)
   Alumno NO ha visto el capítulo de índices todavía en ESTA sesión.
   
   PROMPT AL ALUMNO:
   "¿Cuándo usarías un índice en tu base de datos?
    Piensa en un ejemplo real de tu trabajo.
    Sin consultar nada — solo tu conocimiento previo.
    Escribe 2-3 oraciones."
   
   TIEMPO ESTIMADO: 3 minutos
   ASISTENCIA PERMITIDA: Unaided
   
   ✓ Activation: alumno recupera si sabe
   ✓ CapturedBeforeAssistance: true (ANTES de ver el guion)

─────────────────────────────────────────────

2. PREDICTION (P1 Prediction-error)
   
   PROMPT AL ALUMNO:
   "Now I'll show you two queries:
    Query A: SELECT * FROM users WHERE email = 'john@example.com'
    Query B: SELECT * FROM users WHERE age > 30
    
    Predict: which one would benefit MORE from an index?
    Why? What's your hypothesis?"
   
   TIEMPO ESTIMADO: 2 minutos
   ASISTENCIA PERMITIDA: Unaided
   
   ✓ Prediction ANTES de la explicación
   ✓ Se guardará para comparar vs. resultado real en Reflection

─────────────────────────────────────────────

3. CHAPTERS: PRESENTACIÓN ESTRUCTURADA (P6 Consolidation + P3 Desirable difficulty)
   
   CAPÍTULO 1: "Index Fundamentals" (5 min)
   ───────────────────────────────
   Content:
   - An index is an auxiliary data structure
   - Maps values to row locations (B-tree, hash, bitmap)
   - Trade-off: faster SELECT, slower INSERT/UPDATE
   - Example: library card catalog (physical analogy)
   
   Chapter Recall:
     "After what you just read, what's the main trade-off of an index?"
     (Alumno DEBE responder antes de ver siguiente capítulo)
   
   Chapter Prediction (N/A for this chapter — not primary-weight)
   
   Chapter MiniPractice (OFF-APP):
     "Take 2 minutes. Sketch on paper: how would an index help
      when looking up users by email? You don't need to submit —
      just reflect."
   
   ───────────────────────────────
   CAPÍTULO 2: "When to Index" (5 min)
   ───────────────────────────────
   Content:
   - Indexed columns are usually: PK, FK, WHERE filters, ORDER BY
   - Avoid indexing: rarely-used columns, low-cardinality columns
   - Cost: storage, maintenance during writes
   
   Chapter Recall:
     "Which of these should you index?
      (a) User.CreatedDate (rarely queried)
      (b) User.Email (frequent lookup)
      (c) Order.Status (only 3 values: pending, done, failed)
      Why?"
   
   Chapter Prediction (PRIMARY WEIGHT):
     "Imagine you have a Students table with 50K rows.
      You query by last_name frequently.
      Predict: CREATE INDEX idx_last_name — good idea or bad?
      Defend your answer (2-3 sentences)."
   
   Chapter MiniPractice:
     "On paper, list 3 columns you'd index in your own database.
      Explain each choice (1 sentence per column)."
   
   ───────────────────────────────
   CAPÍTULO 3: "Syntax & Real Examples" (5 min)
   ───────────────────────────────
   Content:
   - CREATE INDEX syntax
   - Single vs. composite indexes
   - Name conventions (idx_tablename_column)
   - Real example: CREATE INDEX idx_users_email ON users(email);
   
   Chapter Recall:
     "What does the word after ON mean in the CREATE INDEX syntax?"
   
   Chapter Prediction (N/A)
   
   Chapter MiniPractice:
     "Write a valid CREATE INDEX for a date column. Don't submit yet."

─────────────────────────────────────────────

4. LEARNER PRODUCTION (P7 Anti-offloading + P4 Encoding specificity)
   
   TASK:
   "Write a CREATE INDEX statement for your own database.
    
    Requirements:
    - Choose a real table and column from your work
    - Index name must follow: idx_tablename_column
    - Add a one-line comment explaining WHY this index helps
    - Execute it (or paste the SQL and screenshot the success)
    
    Submit: [code + screenshot OR database screenshot]"
   
   ASISTENCIA PERMITIDA: Unaided (hints only, never code completion by AI)
   WHAT GETS SAVED: StudentEvidence.Origin = Production
   
   ✓ Auténtica creación del alumno
   ✓ Real schema (Encoding specificity)
   ✓ No AI co-pilot (AssistanceLevel MUST be Unaided)

─────────────────────────────────────────────

5. ASSESSMENT (P1 Error correction)
   
   TutorAgent evaluates against SuccessCriteria:
   ✅ Syntax is correct (SQL parses without error)
   ✅ Index is appropriate for the column (not on constant, not duplicate)
   ✅ Comment justifies the choice (not "because you asked", actual reasoning)
   
   If ALL ✅: Verified
   If ANY ❌: NotVerified
   
   Feedback example:
   "Your syntax is correct ✓, but your index is on an ID column that's
    already a PK. That column doesn't need an additional index.
    Revise: choose a different column that's frequently queried."

─────────────────────────────────────────────

6. REFLECTION (P1 Prediction-error + metacognition)
   
   PROMPT:
   "Earlier, you predicted: 'I'd index the Email column because lookups
    are fast.' Your statement was CORRECT. This module confirmed your
    prediction — you already had good intuition about indexing.
    
    Reflect: what did you learn that was DIFFERENT from what you predicted?"
   
   ASISTENCIA PERMITIDA: Unaided
   
   ✓ Cierra el loop: prediction vs. reality

─────────────────────────────────────────────
```

**Memory Paradox EN ACCIÓN:**
- RECALL comes BEFORE seeing the chapter.
- PRODUCTION: IA NUNCA lo hace, alumno solo.
- REFLECTION: compara lo que pensaba vs. lo que pasó.

---

#### **AGENTE 4: MÉTRICO**

**Qué recibe:**
- Guion del Instructor (estructura arriba)
- SuccessCriteria aprobados
- TargetMetric del módulo

**Qué VERIFICA:**

Para cada criterio de éxito, busca EVIDENCIA en el guion:

```
SuccessCriterion: "Syntax is correct (SQL parses without error)"
├─ ¿Dónde en el guion se valida esto?
├─ Assessment stage: "TutorAgent evaluates: SQL parses without error"
├─ ¿Es observable? SÍ (SQL es o no es válido)
└─ Evidence: "ASSESSMENT stage evaluates StudentEvidence with deterministic SQL parser"

SuccessCriterion: "Index is appropriate for the column"
├─ ¿Dónde se valida?
├─ Assessment stage: "Index not on constant, not duplicate"
├─ ¿Es observable? SÍ (por reglas determinísticas)
└─ Evidence: "ASSESSMENT checks cardinality + already_indexed flags"

SuccessCriterion: "Comment justifies the choice"
├─ ¿Dónde se valida?
├─ Assessment stage: "TutorAgent LLM evaluates comment"
├─ ¿Es observable? SÍ (LLM judge + deterministic validator)
└─ Evidence: "ASSESSMENT LLM+ validator checks: not generic, references specific column/use case"
```

**VERIFICA RECALL CORRECTAMENTE:**
- Recall ocurre ANTES de capítulos? ✅ En guion: "RECALL FIRST"
- Recall es sin pistas? ✅ "Alumno NO ha visto el capítulo todavía"
- AssistanceLevel validado? ✅ "Unaided"
- CapturedBeforeAssistance = true? ✅ "ANTES de ver el guion"

**Resultado:**
```
MetricVerification:
├─ TargetMetric: Application
├─ Status: Verified (todas evidencias presentes)
├─ Evidence: "Production task requires real schema, not generic example"
├─ Recall: "WithoutCues, occurs before chapters" ✓
└─ Explanation: "Module teaches when to index, learner creates index for real table, assessment validates against multiple criteria"
```

Si algún criterio NO se ve evidencia → Status = NotVerified.

---

#### **AGENTE 5: EXPERIENCIA**

**Qué recibe:**
- TODOS los módulos (Verified + RequiresRevision/Failed)
- Corpus original

**Qué HACE:**

1. Ensambla el `CapabilityPackage`:
   ```csharp
   CapabilityPackage
   {
     Capability = new Capability { Name = "Database Optimization", ... },
     Levels = [ Foundation, Exploration, Mastery ],
     Modules = [ Module1, Module2, ..., ModuleN ],
     ModuleVerifications = [ Verified, NotVerified, ... ]  // auditoría
   }
   ```

2. Genera `TutorKnowledgeBase` (para RAG):
   - Toma TODOS los guiones de módulos Verified.
   - Los trocea en chunks (~1000 tokens cada uno).
   - Los embebe con text-embedding-ada-002 (Azure OpenAI).
   - Almacena en `CapabilityKnowledgeChunk` SQL table (con columna VECTOR).
   - El TutorAgent, durante Runtime, consultará esto vía `VECTOR_DISTANCE` SQL.

3. Redacta resumen pedagógico:
   ```
   "This capability teaches database optimization across 3 levels.
    Learners start with fundamentals (what is an index),
    progress to practical application (create indexes),
    and master performance analysis. The knowledge base
    supports real-time tutoring via RAG."
   ```

---

### Las 7 métricas y qué significa "verificada" por Studio

| Métrica | Studio verifica... |
|---|---|
| **Knowledge** | ¿El guion pide que el alumno EXPLIQUE (con sus palabras) el concepto? |
| **Recall** | ¿El alumno RECUPERA SIN PISTAS, antes de instrucción? |
| **Application** | ¿El alumno APLICA a un caso real/diferente del ejemplo dado? |
| **Confidence** | ¿El guion pide que el alumno prediga confianza Y se compara contra resultado real? |
| **Independence** | ¿La tarea de Production tiene AssistanceLevel = Unaided (sin IA)? |
| **Retention** | ¿Se programa repaso espaciado (SM-2, próximo reintento días después)? |
| **Fluency** | ¿El alumno ejecuta automáticamente (sin parar a pensar)? ← solo en niveles altos |

---

### El Memory Paradox aplicado a Studio

```
PROBLEMA: Un agente podría generar
  "Lee esto → resuelve quiz → completado ✓"
  (consumo, no aprendizaje)

SOLUCIÓN EN STUDIO:

1. CuradorAgent NUNCA inventa hechos.
2. ArquitectoAgent define contrato ANTES (Recall, Prediction, Production).
3. InstructorAgent IMPLEMENTA explícitamente:
   ├─ Recuperación ANTES de instrucción
   ├─ Predicción ANTES de resultado
   ├─ Producción AUTÉNTICA (no ejemplo)
   └─ Reflexión metacognitiva
4. MetricoAgent VERIFICA que todo eso realmente está en el guion.
5. ExperienciaAgent NO revisa nada pedagógico — solo ensambla.

RESULTADO:
  Un módulo que llega al Runtime tiene GARANTIZADO:
  ├─ RecallRequirement es recuperable (Curador lo extrajo del corpus)
  ├─ LearnerProduction es creación auténtica (Instructor lo exige)
  ├─ SuccessCriteria son observables (Métrico lo verificó)
  └─ Ningún paso fue "relleno consumo"
```

---

### Checkpoints: de Studio a Runtime

Cuando un módulo se publica (post-GATE 2):

```
SQL Database
├─ CapabilityModule
│  ├─ Script (guion completo con structure)
│  ├─ TargetMetric
│  ├─ RecallRequirement
│  ├─ LearnerProduction
│  ├─ SuccessCriteria (JSON array)
│  ├─ Chapters (JSON array, si aplicable)
│  └─ HasChapters (bool)
│
├─ CapabilityModuleVerification (auditoría de Studio)
│  ├─ TargetMetric verificada
│  ├─ Status (Verified / NotVerified)
│  ├─ Evidence (qué vio el Métrico)
│  ├─ Recall (status + ubicación)
│  └─ EvidenceLocation (línea del guion)
│
└─ CapabilityKnowledgeChunk (para RAG del Tutor)
   ├─ Troceado del guion
   ├─ Embedding vector (1536 dimensiones)
   └─ Metadata (module_id, chunk_index)

Runtime carga CapabilityModule
├─ Extrae RuntimePedagogicalContract (el contrato)
├─ Consulta CapabilityKnowledgeChunk vía RAG si TutorAgent necesita contexto
└─► sesión comienza (alumno ve RecallRequired first)
```

---

---

## Flujo de una sesión de aprendizaje

### Paso a paso de qué ocurre cuando un alumno abre un módulo:

```
1. ALUMNO abre la app → llama a /api/runtime/start-session
                        ├─ PersonId, CapabilityModuleId
                        └─► Backend carga CapabilityModule del SQL
                            
2. Backend inicia RuntimeSession
                        ├─ Carga RuntimePedagogicalContract (del módulo)
                        ├─ Crea RuntimeWorkflowCheckpoint en SQL
                        └─► devuelve EvidenceRequest para Stage = RecallRequired
                            
3. FRONTEND recibe: "Recupera [RecallRequirement] sin consultar nada"
                        ├─ Alumno escribe su respuesta
                        └─► llama a /api/runtime/submit-evidence
                        
4. Backend recibe StudentEvidence
                        ├─ Valida EvidenceAssistanceLevel (Unaided? WithCues?)
                        ├─ Guarda en SQL (Evidence + CapabilityEvidence)
                        ├─ Resume Workflow desde checkpoint anterior
                        └─► transición a Stage = PredictionRequired
                        
5. FRONTEND recibe: "Predice cómo aplicarías esto..."
                        ├─ Alumno predice
                        └─► submit-evidence (StudentEvidenceOrigin.Prediction)
                        
6. Backend transición → Stage = ChapterTeaching (si hay chapters)
                        ├─ devuelve contenido del primer capítulo
                        └─► NO pide evidencia, solo presenta
                        
7. FRONTEND muestra: contenido pedagógico (texto, video, imagen, ...)
                        ├─ Alumno estudia
                        └─► click en "Siguiente"
                        
8. Backend transición → Stage = ChapterRecall (per-chapter retrieval check)
                        ├─ devuelve RecallPrompt del capítulo
                        └─► alumno responde
                        
9. Backend transición → Stage = ChapterPrediction (solo chapter con IsPrimaryWeight=true)
                        └─► alumno predice aplicación
                        
10. Backend transición → Stage = ChapterMiniPractice
                        ├─ devuelve mini-ejercicio (ej. 3 preguntas rápidas)
                        ├─ Alumno trabaja OFF-APP (en cuaderno)
                        └─► devuelve "Completé" (sin evidencia a registrar)
                        
11. Loop: ¿Más capítulos? → vuelve al paso 6
                        NO → Stage = LearnerProduction
                        
12. Backend transición → Stage = LearnerProduction
                        ├─ devuelve: "Ahora CREA [LearnerProduction]"
                        ├─ Alumno envía artefacto (code, documento, proyecto, ...)
                        └─► submit-evidence (StudentEvidenceOrigin.Production)
                        
13. Backend transición → Stage = Assessment
                        ├─ TutorAgent evalúa StudentEvidence contra SuccessCriteria
                        ├─ Deterministic validator comprueba resultado
                        ├─ Si Verified → Reflection, si NotVerified → RequiresRevision
                        └─► devuelve juicio + feedback
                        
14. Backend transición → Stage = Reflection
                        ├─ compara Prediction vs. resultado real
                        └─► devuelve Reflection prompt
                        
15. Alumno responde → submit-evidence (StudentEvidenceOrigin.Reflection)
                        
16. Backend transición → Stage = Completed / RequiresRevision (terminal)
                        └─► sesión termina, marca progresión en PersonCapability
```

---

## StudentEvidence: la prueba del aprendizaje

### ¿Qué es?

Una pieza de **evidencia observable** que prueba que el alumno aprendió algo. NO es "visto", NO es "pasó el tiempo", es **algo que produjo el alumno**.

### Estructura

```csharp
public class StudentEvidence
{
    public Guid StudentEvidenceId { get; set; }
    public Guid RuntimeSessionId { get; set; }
    public Guid CapabilityModuleId { get; set; }
    public Guid PersonId { get; set; }
    
    // ORIGEN: de cuál stage viene esta evidencia
    public StudentEvidenceOrigin Origin { get; set; }  
    // = Recall | Prediction | Production | Reflection
    
    // ASISTENCIA: cuánta ayuda tuvo
    public EvidenceAssistanceLevel AssistanceLevel { get; set; }
    // = Unaided | WithRetrievalCues | WithGuidedHints | WithAiAssistance
    
    // CAPTURA: ¿fue ANTES de que le dieran la respuesta?
    public bool CapturedBeforeAssistance { get; set; }
    // = true para Recall/Prediction SIEMPRE
    
    // CONTENIDO: qué produjo (múltiples formatos)
    public List<StudentEvidencePart> Parts { get; set; }
    // Cada Part: Kind (Text/Image/Code/...) + Text/Url + MimeType
    
    // CONEXIONES: linkea a otras evidencias
    public Guid? ComparesToEvidenceId { get; set; }
    // Reflection compara contra la Prediction anterior
    
    public DateTime CapturedDate { get; set; }
}
```

### Los 4 Origins (orígenes de evidencia)

| Origin | Stage | Significado | Nota |
|---|---|---|---|
| **Recall** | RecallRequired, ChapterRecall | Alumno RECUPERA de memoria sin ayuda | Memory Paradox: ocurre ANTES de instrucción |
| **Prediction** | PredictionRequired, ChapterPrediction | Alumno PREDICE cómo aplicará lo aprendido | P1: prediction-error learning |
| **Production** | LearnerProduction | Alumno CREA el artefacto requerido | Anti-offloading: IA nunca lo hace por él |
| **Reflection** | Reflection | Alumno COMPARA su predicción vs. resultado | P1: metacognición + error correction |

### Los 4 Assistance Levels (ayuda recibida)

| Level | Significado | Cuenta como "aprendizaje"? |
|---|---|---|
| **Unaided** | Cero ayuda | ✅ SÍ, máxima confianza |
| **WithRetrievalCues** | Palabras clave/categorías nada más | ✅ SÍ, sigue siendo recuperación genuina |
| **WithGuidedHints** | Ejemplos, scaffolds, checklists | ⚠️ Cuestionable (depende métrica) |
| **WithAiAssistance** | IA contribuyó al contenido | ❌ NO, rojo flags — es dependencia |

---

## El Tutor Agent (TutorAgent)

### Arquitectura

```csharp
public sealed class TutorAgent  // namespace HumanOS.Agents.Runtime
{
    // Harness-based (Microsoft.Agents.AI)
    // Con Skills pedagógicas, NO autoridades autónomas
    
    // UN SOLO agente para todo el runtime
    // Esto evita handoff-latency en cada turno
}

// Skills que el Tutor posee:
// - RecallSkill: pide recuperación SIN revelar
// - PredictionSkill: pide predicción ANTES de resultado
// - ApplicationSkill: pide aplicación del conocimiento
// - ConfidenceSkill: calibra confianza del alumno
// - IndependenceSkill: fuerza ejecución sin ayuda
// - AssessmentSkill: juicio sobre SuccessCriteria
// - ReflectionSkill: compara predicción vs. realidad
```

### Restricciones de seguridad (Memory Paradox enforced)

El TutorAgent está **deliberadamente amordazado** para prevenir que intente eludir el Memory Paradox:

```csharp
// DESHABILITADO en TutorAgent:
// - Búsqueda web en vivo (host.web_search)
// - Acceso a archivos del filesystem
// - Modo "Agent Mode" autónomo
// - Todo-list provider (no gestión de tareas del alumno por la IA)

// HABILITADO solo:
// - Skills pedagógicas (RecallSkill, PredictionSkill, ...)
// - Lectura de TutorKnowledgeBase vía RAG (SQL VECTOR_DISTANCE)
// - Respuestas conversacionales pedagógicamente soundness
```

### Flujo de una respuesta del Tutor

```
RuntimeStageExecutor → TutorTurnContextBuilder

Construye TutorTurnContext:
├─ Current Stage (RecallRequired, Assessment, ...)
├─ RuntimePedagogicalContract (el contrato del módulo)
├─ StudentEvidence capturada hasta ahora
├─ Qué skills/tools están permitidos ESTE turno
└─► TutorAgent.RespondAsync(context)

TutorAgent ejecuta:
├─ Lee context
├─ Invoca la Skill apropiada (RecallSkill, AssessmentSkill, ...)
├─ Consulta TutorKnowledgeBase vía RAG si necesita contexto
├─ Genera respuesta SIN romper el contrato pedagógico
└─► devuelve TutorAgentResult (solo text, sin autoridad de decisión)

Backend:
├─ TutorAgentResult es una RECOMENDACIÓN, no una decisión
├─ RuntimeStageExecutor decide si la respuesta es válida
├─ Si Assessment dice "Verified", el executor transiciona a Reflection
├─ Si Assessment dice "NotVerified", el executor decide retry o RequiresRevision
└─► respuesta final al frontend
```

---

## Stages y transiciones de estado

### Los 12 Stages (RuntimeStage enum)

```csharp
public enum RuntimeStage
{
    // INICIALIZACIÓN
    ModuleStarted,                  // sesión creada, nada más
    
    // RECUPERACIÓN (Memory Paradox: ANTES de instrucción)
    RecallRequired,                 // recupera SIN ayuda
    PredictionRequired,             // predice SIN ver resultado
    
    // PRESENTACIÓN (no produce evidencia)
    Instruction,                    // presentación completa del módulo (legacy, si no hay Chapters)
    ChapterTeaching,                // presentación de UN capítulo
    ChapterRecall,                  // recuperación per-chapter
    ChapterPrediction,              // predicción de un capítulo (solo la primary-weight)
    ChapterMiniPractice,            // mini-ejercicio off-app
    
    // PRODUCCIÓN (alumno crea artefacto)
    LearnerProduction,              // crea lo que exige LearnerProduction
    
    // EVALUACIÓN (Tutor Agent juzga)
    Assessment,                     // Tutor verifica contra SuccessCriteria
    
    // REFLEXIÓN (metacognición)
    Reflection,                     // compara predicción vs. resultado
    
    // TERMINALES
    Completed,                      // verificado, módulo completo
    RequiresRevision                // no verificado, pendiente atención
}
```

### Máquina de estados

```
ModuleStarted
    ↓
RecallRequired  ──[pausa]──► [alumno responde]
    ↓
PredictionRequired ──[pausa]──► [alumno responde]
    ↓
¿ Has Chapters ? 
    │
    ├─ SÍ: ChapterTeaching → ChapterRecall → ChapterPrediction? → ChapterMiniPractice
    │       ↓
    │       ¿Más capítulos?
    │       ├─ SÍ: vuelve a ChapterTeaching
    │       └─ NO: continúa
    │
    └─ NO: [salta a LearnerProduction directamente]
    
    ↓
LearnerProduction ──[pausa]──► [alumno produce artefacto]
    ↓
Assessment ──[Tutor valida]──
    ├─ Verified ──→ Reflection
    └─ NotVerified ──→ RequiresRevision (terminal alternativo)
    
Reflection ──[pausa]──► [alumno reflexiona]
    ↓
Completed (terminal)
```

---

## Assessment y validación

### Cómo funciona Assessment

Es el corazón de la verificación del aprendizaje.

```
1. TutorAgent entra a Stage = Assessment
   ├─ Lee RuntimePedagogicalContract.SuccessCriteria (ej. 3-5 criterios)
   ├─ Lee StudentEvidence capturada (Recall, Prediction, Production, ...)
   └─► genera juicio LLM sobre cada criterio
   
2. LLMAssessmentResult
   ├─ para cada criterio:
   │  ├─ criterion: string
   │  ├─ isSatisfied: bool
   │  ├─ evidence: string (dónde en StudentEvidence se ve)
   │  └─ explanation: string (por qué sí/no)
   └─► lista completa de juicios
   
3. AssessmentValidator (deterministic code, NO LLM)
   ├─ verifica: ¿TODOS los criterios están isSatisfied = true?
   ├─ verifica: ¿StudentEvidence.AssistanceLevel es válida para TargetMetric?
   ├─ verifica: ¿la evidencia de Production no tiene WithAiAssistance?
   └─► Status = Verified o NotVerified
   
4. Si Status = Verified:
   ├─ TutorAgent genera Reflection prompt
   ├─ Alumno reflexiona
   └─► Stage = Completed, progresión marcada
   
5. Si Status = NotVerified (MAX 2 retries):
   ├─ RuntimeSessionWorkflowFactory.MaxRetries = 2
   ├─ modulo se intenta de nuevo (Instructor regenera en Studio)
   └─► después de 2 retries agotados → RequiresRevision (terminal)
```

### Validación por métrica

Cada `TargetMetric` tiene reglas específicas para qué cuenta:

| Métrica | Qué se necesita para Verified |
|---|---|
| **Knowledge** | StudentEvidence.Origin = Production, esquema conceptual propia del alumno (explicación, mapa, clasificación) |
| **Recall** | StudentEvidence.Origin = Recall, AssistanceLevel ≤ WithRetrievalCues, CapturedBeforeAssistance = true |
| **Application** | StudentEvidence.Origin = Production, aplicado a caso real/situación específica |
| **Confidence** | StudentEvidence con "confianza predicha" + desempeño real + comparación explícita |
| **Independence** | StudentEvidence.Origin = Production, SIN pasos/ejemplos/pistas/IA, AssistanceLevel = Unaided |
| **Retention** | StudentEvidence capturada DESPUÉS de intervalo real (Paso futuro: SM-2 spaced repetition) |
| **Fluency** | StudentEvidence consistente, precisa, adaptable — no una respuesta aislada |

---

## Persistencia y checkpoints

### Problema sin checkpoints

```
Alumno abre módulo en Monday 10:00 AM
├─ CapabilityModule se carga desde SQL
├─ RuntimeSession se crea en memoria
├─ Alumno completa RecallRequired
└─► servidor se reinicia a las 2 PM (actualización, crash, ...)

Alumno vuelve Tuesday 3:00 PM
└─► RuntimeSession PERDIDA, debe empezar de nuevo
    (pérdida de progreso, frustración, Memory Paradox roto)
```

### Solución: SqlRuntimeCheckpointStore

```
1. Cada transición de Stage se persiste en SQL

RuntimeWorkflowCheckpoint
├─ SessionId: uuid del alumno + módulo
├─ CheckpointId: checkpoint.Id del Workflow
├─ ParentCheckpointId: para cadenas de retry
├─ PayloadJson: RuntimeSessionState serializado COMPLETO
│  ├─ Stage actual
│  ├─ Todas las StudentEvidence capturadas
│  ├─ Historial de transiciones
│  └─ Contexto pedagógico
└─ CreatedDate: auditoría

2. Si servidor reinicia, RuntimeApiEngine.ResumeSessionAsync
   ├─ Lee CheckpointId de sesión activa
   ├─ Recupera PayloadJson del SQL
   ├─ Deserializa RuntimeSessionState
   ├─ Resume Workflow EXACTAMENTE donde estaba
   └─► alumno no pierde nada

3. Checkpoints son "append-only"
   ├─ Nunca se sobrescriben
   ├─ Auditoría completa de todas las transiciones
   └─ Historial para análisis de aprendizaje futuro
```

### Pasos en código

```csharp
// En RuntimeStageExecutor (cada transición):
await context.QueueStateUpdateAsync(
    new RuntimeSessionState 
    { 
        Session = updatedSession,
        History = [...previousHistory, newTransition]
    });

// El Workflow automáticamente:
// ├─ crea un checkpoint en ICheckpointStore<JsonElement>
// ├─ el store es SqlRuntimeCheckpointStore (implementación custom)
// └─ registra en RuntimeWorkflowCheckpoint tabla SQL

// Si cliente resume:
var checkpointInfo = /* de sesión anterior */;
var state = await checkpointManager.RetrieveCheckpointAsync(...);
// RuntimeSessionState deserializado
// Workflow resume desde exactamente ese punto
```

---

## Flujo completo de API (HTTP endpoints)

### `POST /api/runtime/start-session`

```json
{
  "personId": "uuid",
  "capabilityModuleId": "uuid"
}
```

**Backend:**
1. Carga `CapabilityModule` del SQL.
2. Extrae `RuntimePedagogicalContract` (TargetMetric, RecallRequirement, LearnerProduction, SuccessCriteria, Chapters).
3. Crea `RuntimeSession` con Stage = `ModuleStarted`.
4. Crea `RuntimeWorkflowCheckpoint` en SQL.
5. Transiciona a Stage = `RecallRequired`.
6. **Respuesta:**

```json
{
  "runtimeSessionId": "uuid",
  "currentStage": "RecallRequired",
  "evidenceRequest": {
    "requestId": "uuid",
    "stage": "RecallRequired",
    "prompt": "Recupera sin consultar: {{RecallRequirement}}",
    "allowedAssistanceLevels": ["Unaided", "WithRetrievalCues"],
    "maxLengthChars": 5000
  }
}
```

---

### `POST /api/runtime/submit-evidence`

```json
{
  "runtimeSessionId": "uuid",
  "evidenceParts": [
    {
      "kind": "Text",
      "text": "Mi respuesta...",
      "mimeType": "text/plain"
    }
  ],
  "assistanceLevel": "Unaided",
  "capturedBeforeAssistance": true
}
```

**Backend:**
1. Deserializa `RuntimeSessionState` del checkpoint anterior.
2. **Mapea** automáticamente (NO confía en el cliente):
   - `StudentEvidenceOrigin` = `MapStageToOrigin(pendingRequest.Stage)`.
   - `CapturedBeforeAssistance` = valida según `Origin` (debe ser true para Recall/Prediction).
3. Crea `StudentEvidence` en memoria, valida `AssistanceLevel`.
4. Guarda en SQL:
   - `Evidence` (tabla principal).
   - `CapabilityEvidence` (enlace con PersonCapability).
5. Reanuda Workflow desde checkpoint anterior.
6. Transiciona al siguiente Stage (ej. RecallRequired → PredictionRequired).
7. **Respuesta:**

```json
{
  "success": true,
  "nextStage": "PredictionRequired",
  "evidenceRequest": {
    "prompt": "Predice cómo aplicarías {{RecallRequirement}}...",
    ...
  }
}
```

---

### `GET /api/runtime/session/{sessionId}`

**Backend:**
1. Recupera `RuntimeWorkflowCheckpoint` más reciente.
2. Deserializa estado.
3. Devuelve estado actual (para UI).

```json
{
  "runtimeSessionId": "uuid",
  "personId": "uuid",
  "currentStage": "LearnerProduction",
  "capturedEvidence": [
    {
      "origin": "Recall",
      "assistanceLevel": "Unaided",
      "parts": [...]
    },
    {
      "origin": "Prediction",
      "assistanceLevel": "WithRetrievalCues",
      "parts": [...]
    }
  ],
  "history": [...]
}
```

---

## Flujo de Studio vs. Runtime (resumen)

| Aspecto | Studio | Runtime |
|---|---|---|
| **Cuándo** | Offline, una sola vez | Online, mientras alumno aprende |
| **Agentes** | 5: Curador, Arquitecto, Instructor, Métrico, Experiencia | 1: TutorAgent + Skills |
| **Entrada** | Material crudo (PDF, video, notas) | Alumno responde y produce evidencia |
| **Salida** | CapabilityModule + TutorKnowledgeBase (embeddings) | Progresión, calificaciones, StudentEvidence |
| **Decisiones** | ¿Cuál es el contrato pedagógico? | ¿Verificó el alumno la métrica? |
| **Checkpoint** | Gates humanos (GATE 1, GATE 2) | Checkpoints técnicos en SQL |

---

## Recapitulación: Memory Paradox en acción

```
PROBLEMA TRADICIONAL:
  Alumno ve video (30 min)
    → Completa quiz rápido (5 min)
    → "módulo completado" ✓
    → Backend: ValidationStatus = "Completed"
    → Realidad: No aprendió nada, solo pasó pruebas superficiales

SOLUCIÓN HUMAN OS (Memory Paradox enforced):

1. RECUPERACIÓN FORZADA
   └─► Stage = RecallRequired (ANTES de ver contenido)
       Alumno: "¿Qué recuerdas de la clase anterior?"
       Pero NO ha visto contenido nuevo aún
       ✓ Activation: memoria previa
       
2. PREDICCIÓN SIN SOLUCIÓN
   └─► Stage = PredictionRequired
       Alumno: "Predice cuál será el resultado si..."
       SIN haber visto la respuesta
       ✓ P1: prediction-error learning
       
3. PRESENTACIÓN ESTRUCTURADA POR CAPÍTULOS
   └─► Stage = ChapterTeaching
       Capítulo 1: concepto A (5 min)
       └─ Stage = ChapterRecall: "¿Qué es A?"
       └─ Stage = ChapterPrediction (si primary): "Aplica A a..."
       └─ Stage = ChapterMiniPractice: ejercicio rápido OFF-APP
       Capítulo 2, 3, ... (mismo ciclo)
       ✓ P6: consolidación distribuida
       
4. PRODUCCIÓN REAL (Alumno crea)
   └─► Stage = LearnerProduction
       "Crea un {{LearnerProduction}}"
       IA NUNCA lo hace por él
       StudentEvidence.AssistanceLevel = Unaided SOLO
       ✓ P7: anti-offloading
       
5. EVALUACIÓN RIGUROSA
   └─► Stage = Assessment
       TutorAgent + AssessmentValidator
       TODOS los SuccessCriteria deben cumplirse
       Memoria verificada (Recall sin pistas)
       Producción sin IA
       ✓ Real evidence
       
6. REFLEXIÓN METACOGNITIVA
   └─► Stage = Reflection
       Compara: "Predijiste X, pasó Y, diferencia..."
       ✓ P1: error correction
       
7. TERMINACIÓN CON PROGRESIÓN REAL
   └─► Stage = Completed (si Verified)
       o RequiresRevision (si falta evidencia)
       Backend: NUNCA marca "completado" por tiempo/consumo
       Solo por evidencia verificable
```

---

## Para empezar a debuggear

### Archivos clave que necesitas conocer

```
backend/HumanOS/
├─ Agents/Runtime/
│  ├─ RuntimeSharedTypes.cs          ← Los tipos base (RuntimeStage, StudentEvidence, etc.)
│  ├─ TutorAgent.cs                  ← El único agente vivo
│  ├─ TutorSkill.cs                  ← Skills (placeholder)
│  └─ TutorTurnContext.cs            ← Contexto de un turno
│
├─ Agentic/Runtime/
│  ├─ RuntimeSessionWorkflowFactory.cs ← Construye el grafo (FSM)
│  ├─ RuntimeStageExecutors.cs       ← Lógica de cada transición
│  ├─ RuntimeApiEngine.cs            ← Orquesta sesiones
│  └─ SqlRuntimeCheckpointStore.cs  ← Persistencia en SQL
│
├─ Data/
│  ├─ HumanOsDbContext.cs           ← DbSet<Evidence>, etc.
│  ├─ RuntimeWorkflowCheckpoint.cs  ← Modelo del checkpoint
│  └─ Migrations/                   ← Tablas SQL
│
├─ Models/Evidence/
│  ├─ Evidence.cs                   ← Modelo de una pieza de evidencia
│  └─ CapabilityEvidence.cs         ← Enlace con capacidad
│
├─ Services/
│  ├─ EvidenceService.cs            ← CRUD de evidencia
│  └─ RecallService.cs              ← Tracking de retrieval
│
└─ Api/
   └─ RuntimeController.cs          ← Endpoints HTTP
```

### Comandos útiles

```powershell
# Ver estado actual del SQL
dotnet ef migrations list --startup-project backend/HumanOS.csproj
dotnet ef database update --startup-project backend/HumanOS.csproj

# Ver qué hay en Evidence
SELECT TOP 10 * FROM Evidence ORDER BY CreatedDate DESC;
SELECT TOP 10 * FROM RuntimeWorkflowCheckpoint ORDER BY CreatedDate DESC;

# Ver progresión de un alumno
SELECT * FROM PersonCapability WHERE PersonId = @PersonId;
SELECT * FROM CapabilityEvidence WHERE PersonCapabilityId = @PcId;
```

---

## Preguntas frecuentes

**P: ¿Cuál es la diferencia entre `Evidence` y `StudentEvidence`?**

A: `Evidence` es la tabla SQL (persistida). `StudentEvidence` es el tipo en código del Runtime (vive en checkpoint JSON). Cuando se persiste una `StudentEvidence` a SQL, se crea una fila `Evidence` + una `CapabilityEvidence`.

**P: ¿Por qué el TutorAgent es Harness y no un simple ChatClientAgent?**

A: Harness permite Skills (RecallSkill, PredictionSkill, ...) que encapsulan lógica pedagógica. ChatClientAgent es demasiado simple para eso — no tiene noción de Skills.

**P: ¿Qué ocurre si un alumno intenta "engañar" el AssistanceLevel?**

A: El backend NUNCA confía en el cliente. `RuntimeApiEngine.BuildEvidence()` calcula `StudentEvidenceOrigin` y `CapturedBeforeAssistance` SOLO del `EvidenceRequest.Stage` (servidor), no del payload del cliente. El cliente solo elige el texto + AssistanceLevel.

**P: ¿Cómo se conecta Studio con Runtime?**

A: Studio genera `CapabilityModule` (con Script + SuccessCriteria + Chapters + TutorKnowledgeBase). Runtime carga eso, lo convierte en `RuntimePedagogicalContract`, y lo usa como "ley" del módulo. Si Studio cambia un módulo, Runtime lo ve en la siguiente sesión.

**P: ¿Qué es el Memory Paradox exactamente?**

A: Es el principio de que "la IA debe fortalecer la memoria/autonomía humana, no sustituirla". En código, se ve en:
- No contar "consumo" como aprendizaje.
- Recuperación ANTES de ayuda.
- Producción sin IA (AssistanceLevel = Unaided).
- Validación rigurosa (todos los SuccessCriteria deben cumplirse).

---

**Última actualización:** 2026-07-17  
**Scope:** Backend Runtime interactivo para aprendizaje (Paso 4 del roadmap de Runtime)  
**Stack:** .NET 10, C#, Azure SQL, Azure OpenAI, Microsoft Agent Framework
