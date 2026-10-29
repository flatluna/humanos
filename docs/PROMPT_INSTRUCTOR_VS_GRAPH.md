Tengo dos pipelines de generación de contenido educativo en el mismo backend (.NET / Azure Functions / EF Core / Azure SQL) y necesito decidir cómo se relacionan ANTES de construir cómo el Runtime/Instructor consume el nuevo modelo de grafo. Dame tu análisis y recomendación concreta para cada pregunta, no solo pros/contras genéricos.

## Contexto — Pipeline A (el viejo, ya en producción parcial)
Modelo plano por niveles:
`Capability → CapabilityLevel (Foundation/Exploration/Mastery/...) → CapabilityModule (Script, RecallRequirement, LearnerProduction) → CapabilityModuleVerification (SuccessCriteria)`

Se genera con agentes: Curador → Arquitecto → Instructor → Métrico → Experiencia (Gate 1 tras Arquitecto, Gate 2 antes de publicar).

El Runtime (motor de sesión del alumno, ya implementado y probado end-to-end) consume esto así:
- `RuntimeStage` (máquina de estados determinista en código, NO un LLM): `ModuleStarted → RecallRequired → PredictionRequired → Instruction → LearnerProduction → Assessment → Reflection → Completed` (+ `RequiresRevision` si se agotan reintentos).
- `RuntimePedagogicalContractProjector.Project(CapabilityModule)` construye un `RuntimePedagogicalContract` (TargetMetric, RecallRequirement, LearnerProduction, SuccessCriteria) — la ÚNICA fuente de autoridad pedagógica que el Tutor Agent puede usar, nunca reinterpretada por el LLM.
- Un `TutorAgent` (Harness + Skills-como-texto, NO agentes separados por skill) genera el texto de cada turno según el `RuntimeStage` actual, con permisos de conocimiento/herramientas gateados por el Runtime (`TutorTurnContextBuilder.ComputePermissions(stage)`), nunca por el LLM.
- Assessment = LLM propone veredicto + validador determinista en código decide (nunca el LLM tiene autoridad final).

## Contexto — Pipeline B (el nuevo, recién construido, NO conectado a Runtime todavía)
Modelo de grafo de conocimiento:
`Capability → CapabilityGraph → CapabilityGraphNode (Concept | Skill, con AcademicDefinition/Interpretation/Examples/Applications/IllustrationPrompts) → CapabilityGraphEdge (Requires/BuildsOn entre nodos) → CapabilityGraphNodeIllustration (imágenes ya generadas y en Data Lake)`

Se genera con: Curador → GraphArchitect (extrae el grafo) → generación de ilustraciones (gpt-image-1.5) → ExperienceDesigner, que produce, POR NODO, un `NodeExperienceBlueprint` con EXACTAMENTE 5 `NodeExperienceBlueprintStep` en este orden fijo (el "Memory Paradox"):

1. **Hypothesis** — construido desde Interpretation + Illustrations, predicción antes de enseñar.
2. **Teaching** — construido desde AcademicDefinition + Interpretation + Examples + Illustrations.
3. **Recall** — retrieval activo desde memoria, sin repetir la explicación.
4. **Production** — tarea abierta usando Applications, el alumno decide el "cómo".
5. **Assessment** — criterios observables derivados de Applications/Examples, nunca revela la respuesta.

Ya validado end-to-end contra Azure SQL real (Curador→GraphArchitect→Ilustraciones→Persistencia→ExperienceDesigner, con relectura independiente en un DbContext nuevo).

## Lo que NO está resuelto todavía (y es la razón de este prompt)
1. **¿El grafo reemplaza al modelo de niveles, o coexisten?** ¿`CapabilityGraph`/`NodeExperienceBlueprint` es el sucesor de `CapabilityLevel`/`CapabilityModule` para capabilities nuevas, o son dos sistemas paralelos para casos de uso distintos (p. ej. grafo = conocimiento conceptual, niveles = progresión de habilidades)?
2. **Mapeo de estados**: los 5 steps del Blueprint (Hypothesis/Teaching/Recall/Production/Assessment) casi calzan con el `RuntimeStage` actual (RecallRequired/PredictionRequired/Instruction/LearnerProduction/Assessment) pero no son 1:1 — el Runtime tiene además `Reflection` (que el Blueprint no tiene) y el orden interno difiere (Runtime hace Recall/Prediction ANTES de Instruction; el Blueprint hace Hypothesis→Teaching→Recall). ¿Se ajusta el `RuntimeStage` existente para que Hypothesis sea la nueva "PredictionRequired" y se reordene, o se crea un `RuntimeStage` paralelo específico para sesiones basadas en grafo?
3. **Unidad de sesión**: ¿una `RuntimeSession` corresponde a UN `CapabilityGraphNode` (un blueprint = una sesión corta), o a un recorrido completo del grafo (varios nodos encadenados por `CapabilityGraphEdge`, una sola sesión larga)? Si es lo segundo, ¿quién decide el orden de recorrido — un topological sort sobre los edges Requires/BuildsOn, hecho por un futuro "RuntimeNavigator"?
4. **Proyección del contrato pedagógico**: ¿`RuntimePedagogicalContractProjector` debería ganar un segundo método `Project(NodeExperienceBlueprint)` (paralelo al que ya existe para `CapabilityModule`), produciendo el mismo `RuntimePedagogicalContract` de salida, o el contrato de salida necesita cambiar de forma para representar los 5 steps con sus `IllustrationIndexes`?
5. **Versionado**: un nodo puede tener múltiples blueprints (Standard/Advanced/Visual Learning). ¿Quién elige cuál usa un alumno concreto — una futura Progression Engine, preferencia explícita del alumno, o el primero disponible? ¿Debe el `NodeExperienceBlueprint` ser inmutable/versionado (para que el progreso histórico de un alumno siga apuntando a la versión exacta que vivió, aunque luego se regenere el blueprint)?
6. **Multiplicidad de nodos por sesión de Instructor**: cuando el `TutorAgent` necesita generar el texto real de cada step (hoy usa `ModuleScript`/`Contract` del Pipeline A), ¿debería en su lugar recibir directamente el `NodeExperienceBlueprintStep.Content` ya redactado por ExperienceDesigner (Instructor solo lo narra/adapta), o Instructor debe regenerar contenido nuevo cada vez a partir del `CapabilityGraphNode` crudo (duplicando el trabajo que ya hizo ExperienceDesigner)?

Quiero tu recomendación puntual para cada una de las 6 preguntas — no una explicación genérica de arquitecturas de grafos de conocimiento — asumiendo que el objetivo final es que el Instructor/Runtime actual (ya construido, Harness+Skills, gating determinista, Assessment LLM+validador) consuma `NodeExperienceBlueprint` sin duplicar la máquina de estados que ya existe.
