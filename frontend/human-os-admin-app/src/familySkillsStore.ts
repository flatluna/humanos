export interface CurriculumSkill {
  id: string;
  subjectKey: string;
  subjectNameEs: string;
  subjectNameEn: string;
  code: string;
  nameEs: string;
  nameEn: string;
  descriptionEs: string;
  descriptionEn: string;
  whyItMattersEs: string;
  whyItMattersEn: string;
  skillsCovered: string[];
  hoursToMaster: number;
  selected: boolean;
}

export const INITIAL_KINDERGARTEN_SYLLABUS: CurriculumSkill[] = [
  {
    id: 'k-1',
    subjectKey: 'lenguaje',
    subjectNameEs: 'Lenguaje, lectura y escritura',
    subjectNameEn: 'Language, Reading & Writing',
    code: 'KIND-LIT-01',
    nameEs: 'Conciencia Fonológica, Rimas y Discriminación Auditiva',
    nameEn: 'Phonological Awareness, Rhyming & Sound Discrimination',
    descriptionEs: 'Identificar rimas, contar sílabas con palmadas y aislar el sonido inicial y final de palabras cotidianas.',
    descriptionEn: 'Recognize rhyming words, count syllables by clapping, and isolate beginning and ending sounds in common words.',
    whyItMattersEs: 'Es el predictor número uno del éxito futuro en lectura y ortografía.',
    whyItMattersEn: 'The single strongest predictor of future reading fluency and literacy success.',
    skillsCovered: ['Rimas consonantes y asonantes', 'Conteo de sílabas', 'Aislamiento de sonido inicial', 'Juegos de trabalenguas'],
    hoursToMaster: 35,
    selected: true,
  },
  {
    id: 'k-2',
    subjectKey: 'lenguaje',
    subjectNameEs: 'Lenguaje, lectura y escritura',
    subjectNameEn: 'Language, Reading & Writing',
    code: 'KIND-LIT-02',
    nameEs: 'Reconocimiento del Alfabeto A-Z y Principio Alfabético',
    nameEn: 'Alphabet Recognition A-Z & Letter-Sound Correspondence',
    descriptionEs: 'Nombrar y trazar letras mayúsculas y minúsculas asociándolas con su sonido fonético correspondiente.',
    descriptionEn: 'Identify and write uppercase and lowercase letters, matching each letter to its primary phonetic sound.',
    whyItMattersEs: 'Permite pasar de la decodificación silábica a la lectura fluida de palabras.',
    whyItMattersEn: 'Enables independent word decoding and emergent reading.',
    skillsCovered: ['Letras mayúsculas y minúsculas', 'Sonido fonético de vocales', 'Sonido de consonantes frecuentes', 'Lectura de palabras simples (CVC)'],
    hoursToMaster: 40,
    selected: true,
  },
  {
    id: 'k-3',
    subjectKey: 'matematicas',
    subjectNameEs: 'Matemáticas',
    subjectNameEn: 'Mathematics',
    code: 'KIND-MAT-01',
    nameEs: 'Conteo, Cardinalidad y Sentido Numérico del 1 al 20',
    nameEn: 'Counting, Cardinality & Number Sense 1-20',
    descriptionEs: 'Contar colecciones de hasta 20 objetos con correspondencia uno a uno y reconocer cantidades visuales rápidas (subitización hasta 5).',
    descriptionEn: 'Count up to 20 objects with 1-to-1 correspondence and instantly recognize visual quantities up to 5 without counting (subitizing).',
    whyItMattersEs: 'Construye la comprensión intuitiva de la cantidad antes de pasar al cálculo formal.',
    whyItMattersEn: 'Builds foundational quantity intuition before formal arithmetic.',
    skillsCovered: ['Conteo 1 a 20', 'Correspondencia uno a uno', 'Subitización visual', 'Comparación mayor / menor / igual'],
    hoursToMaster: 35,
    selected: true,
  },
  {
    id: 'k-4',
    subjectKey: 'matematicas',
    subjectNameEs: 'Matemáticas',
    subjectNameEn: 'Mathematics',
    code: 'KIND-MAT-02',
    nameEs: 'Clasificación de Figuras Geométricas y Patrones Lógicos',
    nameEn: '2D/3D Shapes, Sorting & Logical Patterns',
    descriptionEs: 'Identificar círculos, triángulos, cuadrados, rectángulos y cuerpos geométricos; crear y extender patrones lógicos (AB, ABC).',
    descriptionEn: 'Identify circles, triangles, squares, rectangles, and 3D shapes; create and extend repeating visual patterns (AB, ABC).',
    whyItMattersEs: 'Fomenta el razonamiento espacial y el pensamiento algebraico temprano.',
    whyItMattersEn: 'Fosters spatial reasoning and early algebraic logic.',
    skillsCovered: ['Figuras bidimensionales básicas', 'Cuerpos tridimensionales simples', 'Patrones de repetición', 'Clasificación por color y tamaño'],
    hoursToMaster: 25,
    selected: true,
  },
  {
    id: 'k-5',
    subjectKey: 'ciencias',
    subjectNameEs: 'Ciencias',
    subjectNameEn: 'Sciences',
    code: 'KIND-CIE-01',
    nameEs: 'Exploración de la Naturaleza mediante los Cinco Sentidos',
    nameEn: 'Nature Inquiry & The Five Senses',
    descriptionEs: 'Observar, describir y clasificar plantas, animales, texturas y sonidos del entorno natural usando la vista, oído, tacto, olfato y gusto.',
    descriptionEn: 'Observe, describe, and classify natural phenomena, plants, animals, and textures using sensory observation.',
    whyItMattersEs: 'Desarrolla la capacidad de asombro y la formulación de hipótesis científicas tempranas.',
    whyItMattersEn: 'Builds empirical curiosity and early scientific observation habits.',
    skillsCovered: ['Uso de los 5 sentidos', 'Clasificación de objetos vivos y no vivos', 'Registro con dibujos en cuaderno de campo', 'Cuidado de seres vivos'],
    hoursToMaster: 25,
    selected: true,
  },
  {
    id: 'k-6',
    subjectKey: 'estudios_sociales',
    subjectNameEs: 'Estudios sociales',
    subjectNameEn: 'Social Studies',
    code: 'KIND-SOC-01',
    nameEs: 'Mi Familia, Comunidad y Normas de Convivencia Pacífica',
    nameEn: 'Family, Community Helpers & Classroom Citizenship',
    descriptionEs: 'Reconocer el rol de los miembros de la familia, oficios de la comunidad (médicos, bomberos, maestros) y respetar turnos en el grupo.',
    descriptionEn: 'Understand family roles, community helpers (firefighters, doctors, teachers), and practice fairness and turn-taking.',
    whyItMattersEs: 'Sienta las bases de la empatía cívica y la integración armónica en la sociedad.',
    whyItMattersEn: 'Establishes civic empathy and cooperative community belonging.',
    skillsCovered: ['Roles familiares', 'Servidores públicos y oficios', 'Reglas de cortesía y escucha', 'Resolución pacífica de desacuerdos'],
    hoursToMaster: 20,
    selected: true,
  },
  {
    id: 'k-7',
    subjectKey: 'salud_emocional',
    subjectNameEs: 'Salud y bienestar emocional',
    subjectNameEn: 'Health & Emotional Well-being',
    code: 'KIND-SAL-01',
    nameEs: 'Identificación de Emociones y Hábitos de Autorregulación',
    nameEn: 'Emotion Identification & Self-Regulation Habits',
    descriptionEs: 'Nombrar emociones (alegría, enojo, miedo, tristeza), reconocer señales corporales de estrés y usar técnicas de respiración para calmarse.',
    descriptionEn: 'Identify basic emotions in self and others, recognize physical cues of stress, and use deep breathing to regain calm.',
    whyItMattersEs: 'Previene berrinches, mejora la atención en clase y fortalece la resiliencia mental.',
    whyItMattersEn: 'Reduces classroom distress and strengthens foundational mental health resilience.',
    skillsCovered: ['Rueda de emociones básicas', 'Respiración de la flor y la vela', 'Pedir ayuda verbalmente', 'Hábitos de sueño y lavado de manos'],
    hoursToMaster: 25,
    selected: true,
  },
  {
    id: 'k-8',
    subjectKey: 'educacion_fisica',
    subjectNameEs: 'Educación física',
    subjectNameEn: 'Physical Education & Motor Skills',
    code: 'KIND-EFI-01',
    nameEs: 'Motricidad Gruesa, Equilibrio y Coordinación Espacial',
    nameEn: 'Gross Motor Skills, Balance & Spatial Awareness',
    descriptionEs: 'Saltar en un pie, atrapar pelotas con ambas manos, correr esquivando obstáculos y seguir ritmos de movimiento corporal.',
    descriptionEn: 'Hop on one foot, catch large balls with both hands, navigate obstacle courses, and synchronize body movement.',
    whyItMattersEs: 'La motricidad gruesa oxigena el cerebro y es indispensable para la maduración neurológica.',
    whyItMattersEn: 'Gross motor coordination directly stimulates neurological and executive function maturation.',
    skillsCovered: ['Equilibrio dinámico y estático', 'Lanzamiento y recepción de pelota', 'Circuitos de saltos', 'Conciencia del espacio personal'],
    hoursToMaster: 30,
    selected: true,
  },
  {
    id: 'k-9',
    subjectKey: 'arte',
    subjectNameEs: 'Arte',
    subjectNameEn: 'Visual Arts',
    code: 'KIND-ART-01',
    nameEs: 'Motricidad Fina, Trazo, Modelado con Plastilina y Pintura',
    nameEn: 'Fine Motor Skills, Pencil Grip, Clay Sculpting & Painting',
    descriptionEs: 'Uso correcto del agarre en trípode del lápiz, recorte controlado con tijeras punta roma, modelado con plastilina y mezclas de colores primarios.',
    descriptionEn: 'Tripod pencil grasp, cutting along curved lines with blunt scissors, clay molding, and primary color mixing.',
    whyItMattersEs: 'Evita fatiga muscular en la mano y prepara la musculatura para la escritura formal en 1° de primaria.',
    whyItMattersEn: 'Builds intrinsic hand muscles required for handwriting endurance in primary grades.',
    skillsCovered: ['Agarre en trípode del lápiz', 'Recorte con tijeras sobre líneas', 'Modelado tridimensional con plastilina', 'Mezcla de colores primarios'],
    hoursToMaster: 30,
    selected: true,
  },
  {
    id: 'k-10',
    subjectKey: 'musica',
    subjectNameEs: 'Música',
    subjectNameEn: 'Music & Rhythm',
    code: 'KIND-MUS-01',
    nameEs: 'Percepción de Ritmo, Pulso y Canto Infantil',
    nameEn: 'Rhythm Perception, Beat Keeping & Pitch Matching',
    descriptionEs: 'Seguir el pulso con palmas o instrumentos de percusión simples, cantar canciones al unísono y distinguir sonidos agudos de graves.',
    descriptionEn: 'Keep a steady musical beat with claps or simple percussion, sing nursery songs on pitch, and distinguish high vs low sounds.',
    whyItMattersEs: 'Estimula el procesamiento fonológico y la memoria auditiva necesaria para aprender idiomas.',
    whyItMattersEn: 'Directly reinforces auditory memory and phonological brain pathways for languages.',
    skillsCovered: ['Seguimiento del pulso musical', 'Canto coral infantil', 'Diferenciación agudo / grave', 'Instrumentos de percusión menor'],
    hoursToMaster: 20,
    selected: true,
  },
  {
    id: 'k-11',
    subjectKey: 'teatro',
    subjectNameEs: 'Teatro y expresión',
    subjectNameEn: 'Drama & Expression',
    code: 'KIND-TEA-01',
    nameEs: 'Juego Simbólico, Títeres y Dramatización de Cuentos',
    nameEn: 'Imaginative Role-Play, Puppets & Story Enactment',
    descriptionEs: 'Representar personajes de cuentos con gestos, voces y títeres, expresando intenciones y trabajando en equipo.',
    descriptionEn: 'Act out story characters with voice inflection, puppets, and gestures, interpreting dramatic situations with peers.',
    whyItMattersEs: 'Desarrolla la teoría de la mente, la empatía y la seguridad para hablar en público.',
    whyItMattersEn: 'Builds theory of mind, socio-cognitive perspective taking, and confident public speaking.',
    skillsCovered: ['Dramatización de personajes', 'Manejo de títeres de mano', 'Modulación de voz', 'Expresión corporal'],
    hoursToMaster: 20,
    selected: true,
  },
  {
    id: 'k-12',
    subjectKey: 'tecnologia',
    subjectNameEs: 'Tecnología',
    subjectNameEn: 'Technology & Logic',
    code: 'KIND-TEC-01',
    nameEs: 'Pensamiento Computacional Inicial y Secuencias Lógicas',
    nameEn: 'Early Computational Thinking, Unplugged Coding & Logic',
    descriptionEs: 'Seguir y crear algoritmos sin pantallas (juegos unplugged de dar pasos adelante/giros), causa-efecto y uso seguro y guiado de dispositivos.',
    descriptionEn: 'Follow and design unplugged algorithms (giving directional step commands), understand cause-effect, and safe device etiquette.',
    whyItMattersEs: 'Enseña al niño a pensar de manera lógica, descomponer problemas y ser creador de tecnología.',
    whyItMattersEn: 'Teaches problem decomposition, sequencing logic, and confident digital citizenship.',
    skillsCovered: ['Algoritmos desconectados (unplugged)', 'Instrucciones paso a paso', 'Causa y efecto', 'Reglas de tiempo de pantalla'],
    hoursToMaster: 20,
    selected: true,
  },
  {
    id: 'k-13',
    subjectKey: 'segundo_idioma',
    subjectNameEs: 'Segundo idioma',
    subjectNameEn: 'Second Language (ESL / Dual Language)',
    code: 'KIND-IDI-01',
    nameEs: 'Vocabulario Básico, Saludos y Canciones en Segundo Idioma',
    nameEn: 'Conversational Greetings, Core Vocabulary & Bilingual Chants',
    descriptionEs: 'Comprender y responder a saludos, números, colores, animales e instrucciones sencillas en el segundo idioma (inglés/español).',
    descriptionEn: 'Understand and respond to greetings, colors, numbers, animals, and routine commands in second language.',
    whyItMattersEs: 'Aprovecha la ventana neurológica óptima de pronunciación nativa y bilingüismo fluido.',
    whyItMattersEn: 'Capitalizes on the peak neurological window for native phonetic acquisition and bilingual fluency.',
    skillsCovered: ['Saludos y fórmulas de cortesía', 'Colores y números en segundo idioma', 'Vocabulario de animales y familia', 'Canciones y rimas infantiles bilingües'],
    hoursToMaster: 35,
    selected: true,
  },
];

const STORAGE_PREFIX = 'family_skills_saved_';

export const familySkillsStore = {
  getSavedSkills(stageKey: string): CurriculumSkill[] {
    const raw = localStorage.getItem(`${STORAGE_PREFIX}${stageKey}`);
    if (raw) {
      try {
        const parsed = JSON.parse(raw);
        if (Array.isArray(parsed)) return parsed;
      } catch {
        // Ignored
      }
    }

    // Kindergarten has the 13 official syllabus skills saved by default
    if (stageKey === 'kindergarten') {
      localStorage.setItem(`${STORAGE_PREFIX}kindergarten`, JSON.stringify(INITIAL_KINDERGARTEN_SYLLABUS));
      return INITIAL_KINDERGARTEN_SYLLABUS;
    }

    // For ANY OTHER stage (1st_2nd_grade, 3rd_5th_grade, middle_school, high_school, college, professional):
    // RETURN EMPTY LIST! NO FAKE / INVENTED CARDS!
    return [];
  },

  saveSkills(stageKey: string, skills: CurriculumSkill[]) {
    localStorage.setItem(`${STORAGE_PREFIX}${stageKey}`, JSON.stringify(skills));
  },

  hasSavedSkills(stageKey: string): boolean {
    const list = familySkillsStore.getSavedSkills(stageKey);
    return list.length > 0;
  },
};
