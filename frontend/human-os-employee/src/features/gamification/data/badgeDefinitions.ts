export interface BadgeDetailInfo {
  code: string;
  category: string;
  name: {
    en: string;
    es: string;
  };
  iconEmoji: string;
  ruleDescription: {
    en: string;
    es: string;
  };
  neuroscienceExplanation: {
    en: string;
    es: string;
  };
  howToEarn: {
    en: string;
    es: string;
  };
}

export const BADGE_DEFINITIONS: Record<string, BadgeDetailInfo> = {
  // CATEGORY A — EFFORT MILESTONES
  first_neuron: {
    code: 'first_neuron',
    category: 'Effort',
    name: {
      en: 'First Neuron',
      es: 'Primera Neurona',
    },
    iconEmoji: '🎯',
    ruleDescription: {
      en: 'Master your very first engram across any course or training.',
      es: 'Domina tu primer engram en cualquier curso o capacitación.',
    },
    neuroscienceExplanation: {
      en: 'Provides immediate dopamine reinforcement on initial mastery, triggering synaptic consolidation and overcoming the inertia of starting.',
      es: 'Proporciona refuerzo inmediato de dopamina en el primer logro, activando la consolidación sináptica y superando la inercia del inicio.',
    },
    howToEarn: {
      en: 'Complete and successfully assess any node in a graph course or section in a structured course.',
      es: 'Completa y aprueba con éxito cualquier nodo en un curso de grafo o sección en un curso estructurado.',
    },
  },
  effort_10: {
    code: 'effort_10',
    category: 'Effort',
    name: {
      en: 'Apprentice',
      es: 'Aprendiz',
    },
    iconEmoji: '🌱',
    ruleDescription: {
      en: 'Master 5% of all engrams assigned in your personalized career roadmap (or at least 10 engrams).',
      es: 'Domina el 5% de todos los engrams asignados en tu plan de carrera (o al menos 10 engrams).',
    },
    neuroscienceExplanation: {
      en: 'Builds foundational neural scaffolding. Early repetition sets up hippocampal-neocortical memory transfers.',
      es: 'Construye el andamiaje neural fundacional. La repetición temprana facilita la transferencia de memoria hipocampal-neocortical.',
    },
    howToEarn: {
      en: 'Continue studying and mastering engrams in your assigned courses until reaching the 5% threshold.',
      es: 'Continúa estudiando y dominando engrams en tus cursos asignados hasta alcanzar el umbral del 5%.',
    },
  },
  effort_25: {
    code: 'effort_25',
    category: 'Effort',
    name: {
      en: 'Practitioner',
      es: 'Practicante',
    },
    iconEmoji: '📘',
    ruleDescription: {
      en: 'Master 15% of all engrams assigned in your career roadmap.',
      es: 'Domina el 15% de todos los engrams asignados en tu plan de carrera.',
    },
    neuroscienceExplanation: {
      en: 'Reinforces learning momentum. When a skill crosses the 15% mark, cognitive resistance to studying drops substantially.',
      es: 'Refuerza el impulso de aprendizaje. Al cruzar el 15%, la resistencia cognitiva al estudio disminuye sustancialmente.',
    },
    howToEarn: {
      en: 'Steadily progress across multiple topics and achieve 15% mastery of your total curriculum.',
      es: 'Avanza de forma constante en varios temas y alcanza el 15% de dominio de tu plan de estudios.',
    },
  },
  effort_50: {
    code: 'effort_50',
    category: 'Effort',
    name: {
      en: 'Expert',
      es: 'Experto',
    },
    iconEmoji: '🎓',
    ruleDescription: {
      en: 'Master 30% of all engrams in your learning roadmap.',
      es: 'Domina el 30% de todos los engrams de tu plan de capacitación.',
    },
    neuroscienceExplanation: {
      en: 'Turns repeated effort into automatic recall. Neural pathways are myelinated, speeding up cognitive processing.',
      es: 'Convierte el esfuerzo repetido en recuerdo automático. Las vías neuronales se mielinizan, acelerando el procesamiento cognitivo.',
    },
    howToEarn: {
      en: 'Work through your core job role capabilities until 30% of engrams are mastered.',
      es: 'Trabaja en las capacidades principales de tu puesto hasta dominar el 30% de los engrams.',
    },
  },
  effort_100: {
    code: 'effort_100',
    category: 'Effort',
    name: {
      en: 'Master',
      es: 'Maestro',
    },
    iconEmoji: '🏅',
    ruleDescription: {
      en: 'Master 50% of all engrams assigned in your career roadmap.',
      es: 'Domina el 50% de todos los engrams asignados en tu plan de carrera.',
    },
    neuroscienceExplanation: {
      en: 'The critical halfway milestone. Triggers high intrinsic motivation as the finish line transitions from abstract to concrete.',
      es: 'El logro clave de medio camino. Activa una alta motivación intrínseca al percibir la meta final tangible y alcanzable.',
    },
    howToEarn: {
      en: 'Complete half of all available and assigned knowledge nodes in your curriculum.',
      es: 'Completa la mitad de todos los nodos de conocimiento disponibles y asignados en tu currículo.',
    },
  },
  effort_200: {
    code: 'effort_200',
    category: 'Effort',
    name: {
      en: 'Scholar',
      es: 'Erudito',
    },
    iconEmoji: '💎',
    ruleDescription: {
      en: 'Master 75% of all engrams in your career roadmap.',
      es: 'Domina el 75% de todos los engrams en tu plan de carrera.',
    },
    neuroscienceExplanation: {
      en: 'Represents comprehensive cognitive synthesis. Cross-domain concepts interconnect, creating high-density semantic webs.',
      es: 'Representa una síntesis cognitiva integral. Los conceptos interdisciplinarios se interconectan creando redes semánticas densas.',
    },
    howToEarn: {
      en: 'Reach the home stretch with three-quarters of your assigned curriculum mastered.',
      es: 'Llega a la recta final con tres cuartas partes de tu plan de capacitación dominadas.',
    },
  },
  effort_300: {
    code: 'effort_300',
    category: 'Effort',
    name: {
      en: 'Legend',
      es: 'Leyenda',
    },
    iconEmoji: '👑',
    ruleDescription: {
      en: 'Master 100% of all engrams assigned in your personalized career roadmap.',
      es: 'Domina el 100% de todos los engrams asignados en tu plan de carrera personalizado.',
    },
    neuroscienceExplanation: {
      en: 'The pinnacle of deliberate practice and full neural consolidation across every assigned domain.',
      es: 'La cumbre de la práctica deliberada y consolidación neural total en cada dominio asignado.',
    },
    howToEarn: {
      en: 'Master every single engram in your job-skills, department, and company courses.',
      es: 'Domina hasta el último engram en tus cursos de puesto, departamento y empresa.',
    },
  },

  // CATEGORY B — COURSE PROGRESS
  skill_progress_25: {
    code: 'skill_progress_25',
    category: 'Progress',
    name: {
      en: 'Quarter Way',
      es: 'Primer Cuarto',
    },
    iconEmoji: '🥉',
    ruleDescription: {
      en: 'Complete 25% of any individual course.',
      es: 'Completa el 25% de cualquier curso individual.',
    },
    neuroscienceExplanation: {
      en: 'Overcomes the initial slope of difficulty, preventing early abandonment by rewarding early concept mastery.',
      es: 'Supera la pendiente inicial de dificultad, previniendo el abandono temprano al premiar los primeros conceptos.',
    },
    howToEarn: {
      en: 'Pass enough nodes or lessons to reach one-quarter completion in a single course.',
      es: 'Aprueba suficientes nodos o lecciones para alcanzar un cuarto del progreso en un curso.',
    },
  },
  skill_progress_50: {
    code: 'skill_progress_50',
    category: 'Progress',
    name: {
      en: 'Halfway There',
      es: 'A Medio Camino',
    },
    iconEmoji: '🥈',
    ruleDescription: {
      en: 'Complete 50% of any individual course.',
      es: 'Completa el 50% de cualquier curso individual.',
    },
    neuroscienceExplanation: {
      en: 'Activates the goal-gradient effect: motivation increases as the perceived distance to the goal decreases.',
      es: 'Activa el efecto de gradiente de meta: la motivación se intensifica a medida que la distancia percibida a la meta disminuye.',
    },
    howToEarn: {
      en: 'Master half of all knowledge nodes in any active course.',
      es: 'Domina la mitad de todos los nodos de conocimiento en cualquier curso activo.',
    },
  },
  skill_progress_75: {
    code: 'skill_progress_75',
    category: 'Progress',
    name: {
      en: 'Home Stretch',
      es: 'Recta Final',
    },
    iconEmoji: '🥇',
    ruleDescription: {
      en: 'Complete 75% of any individual course.',
      es: 'Completa el 75% de cualquier curso individual.',
    },
    neuroscienceExplanation: {
      en: 'Harnesses high dopamine arousal and anticipation as the final milestones come into view.',
      es: 'Aprovecha la anticipación y el estímulo dopaminérgico al tener a la vista las últimas lecciones del curso.',
    },
    howToEarn: {
      en: 'Advance to 75% completion in any course by passing its intermediate and advanced topics.',
      es: 'Avanza al 75% de avance en cualquier curso aprobando sus temas intermedios y avanzados.',
    },
  },
  skill_complete_1: {
    code: 'skill_complete_1',
    category: 'Progress',
    name: {
      en: 'Skill Mastered',
      es: 'Habilidad Dominada',
    },
    iconEmoji: '🛡️',
    ruleDescription: {
      en: 'Complete 100% of any individual course.',
      es: 'Completa el 100% de cualquier curso individual.',
    },
    neuroscienceExplanation: {
      en: 'Marks complete episodic-to-semantic memory transformation of an entire knowledge domain.',
      es: 'Marca la transformación completa de memoria episódica a memoria semántica de todo un dominio de conocimiento.',
    },
    howToEarn: {
      en: 'Master every node and section of any course in your curriculum.',
      es: 'Domina cada nodo y sección de cualquier curso de tu plan de formación.',
    },
  },

  // CATEGORY C — SKILLS ACCUMULATED
  skills_1: {
    code: 'skills_1',
    category: 'Skills',
    name: {
      en: 'First Skill',
      es: 'Primera Habilidad',
    },
    iconEmoji: '🏆',
    ruleDescription: {
      en: 'Fully complete your first entire course or skill from your learning plan.',
      es: 'Completa totalmente tu primer curso o habilidad de tu plan de aprendizaje.',
    },
    neuroscienceExplanation: {
      en: 'Demonstrates end-to-end task completion, establishing self-efficacy and confidence for future courses.',
      es: 'Demuestra la culminación completa de una tarea, estableciendo autoeficacia y confianza para futuros cursos.',
    },
    howToEarn: {
      en: 'Finish 100% of all engrams in at least one assigned course.',
      es: 'Finaliza el 100% de todos los engrams en al menos un curso asignado.',
    },
  },
  skills_3: {
    code: 'skills_3',
    category: 'Skills',
    name: {
      en: 'Getting Started',
      es: 'Comenzando',
    },
    iconEmoji: '🌱',
    ruleDescription: {
      en: 'Complete 15% of the courses in your career plan (or at least 3 courses).',
      es: 'Completa el 15% de los cursos de tu plan de carrera (o al menos 3 cursos).',
    },
    neuroscienceExplanation: {
      en: 'Early multi-domain breadth. Transfer learning begins occurring as principles from one course aid another.',
      es: 'Amplitud multidisciplinaria temprana. El aprendizaje de transferencia comienza al aplicar principios de un curso en otro.',
    },
    howToEarn: {
      en: 'Complete 3 complete courses across job, department, or HR tracks.',
      es: 'Completa 3 cursos completos entre las ramas de puesto, departamento o RRHH.',
    },
  },
  skills_6: {
    code: 'skills_6',
    category: 'Skills',
    name: {
      en: 'Building Momentum',
      es: 'Tomando Impulso',
    },
    iconEmoji: '🚀',
    ruleDescription: {
      en: 'Complete 30% of the courses in your career plan (or at least 6 courses).',
      es: 'Completa el 30% de los cursos de tu plan de carrera (o al menos 6 cursos).',
    },
    neuroscienceExplanation: {
      en: 'Cross-functional competency. The brain builds flexible conceptual frameworks spanning varied topics.',
      es: 'Competencia multifuncional. El cerebro estructura marcos conceptuales flexibles que abarcan diversos temas.',
    },
    howToEarn: {
      en: 'Finish 6 complete courses from your assigned catalog.',
      es: 'Termina 6 cursos completos de tu catálogo asignado.',
    },
  },
  skills_10: {
    code: 'skills_10',
    category: 'Skills',
    name: {
      en: 'Halfway Expert',
      es: 'Experto Intermedio',
    },
    iconEmoji: '🥈',
    ruleDescription: {
      en: 'Complete 50% of the courses in your career plan (or at least 10 courses).',
      es: 'Completa el 50% de los cursos de tu plan de carrera (o al menos 10 cursos).',
    },
    neuroscienceExplanation: {
      en: 'Robust mental modeling. Knowledge networks exhibit high resilience against cognitive decay.',
      es: 'Modelado mental robusto. Las redes de conocimiento exhiben alta resistencia contra el deterioro cognitivo.',
    },
    howToEarn: {
      en: 'Complete half of all assigned courses in your career plan.',
      es: 'Completa la mitad de todos los cursos asignados en tu plan de carrera.',
    },
  },
  skills_15: {
    code: 'skills_15',
    category: 'Skills',
    name: {
      en: 'Almost There',
      es: 'Casi Listo',
    },
    iconEmoji: '🥇',
    ruleDescription: {
      en: 'Complete 75% of the courses in your career plan (or at least 15 courses).',
      es: 'Completa el 75% de los cursos de tu plan de carrera (o al menos 15 cursos).',
    },
    neuroscienceExplanation: {
      en: 'Deep expertise and high-order thinking. Mastery is fluid, allowing swift application in workplace scenarios.',
      es: 'Pericia profunda y pensamiento de alto orden. El dominio es fluido, permitiendo rápida aplicación en el entorno laboral.',
    },
    howToEarn: {
      en: 'Complete three-quarters of your full course catalog.',
      es: 'Completa las tres cuartas partes de tu catálogo completo de cursos.',
    },
  },
  skills_all: {
    code: 'skills_all',
    category: 'Skills',
    name: {
      en: 'Skill Master',
      es: 'Dominio Total',
    },
    iconEmoji: '🥷',
    ruleDescription: {
      en: 'Complete 100% of all assigned courses in your career plan.',
      es: 'Completa el 100% de todos los cursos asignados en tu plan de carrera.',
    },
    neuroscienceExplanation: {
      en: 'Total curriculum mastery and comprehensive organizational capability.',
      es: 'Dominio total del currículo y capacidad organizacional integral.',
    },
    howToEarn: {
      en: 'Finish every course assigned to your job role and organization.',
      es: 'Termina todos y cada uno de los cursos asignados a tu puesto y organización.',
    },
  },

  // CATEGORY D — MASTERY & QUALITY
  quality_perfect_10: {
    code: 'quality_perfect_10',
    category: 'Quality',
    name: {
      en: 'Perfectionist',
      es: 'Perfeccionista',
    },
    iconEmoji: '🎯',
    ruleDescription: {
      en: 'Achieve a perfect 100% score on 10 assessment evaluations.',
      es: 'Obtén una calificación perfecta de 100% en 10 evaluaciones.',
    },
    neuroscienceExplanation: {
      en: 'Incentivizes precision over speed, encouraging deep attentional focus and thorough self-checking.',
      es: 'Incentiva la precisión sobre la velocidad, fomentando enfoque atencional profundo y auto-revisión exhaustiva.',
    },
    howToEarn: {
      en: 'Score 100% on assessments 10 distinct times across your studies.',
      es: 'Obtén 100% en evaluaciones en 10 ocasiones distintas a lo largo de tus estudios.',
    },
  },
  quality_firsttry_10: {
    code: 'quality_firsttry_10',
    category: 'Quality',
    name: {
      en: 'One-Shot',
      es: 'A la Primera',
    },
    iconEmoji: '🧠',
    ruleDescription: {
      en: 'Master 10 engrams on the very first attempt without needing a retry.',
      es: 'Domina 10 engrams en el primer intento sin necesidad de reintentar.',
    },
    neuroscienceExplanation: {
      en: 'Rewards high attentional encoding during initial presentation and deep-dive reading steps.',
      es: 'Premia la codificación atencional alta durante la fase inicial de lectura y análisis en profundidad.',
    },
    howToEarn: {
      en: 'Pass 10 different learning assessments on your very first evaluation attempt.',
      es: 'Aprueba 10 evaluaciones de aprendizaje diferentes en tu primer intento de evaluación.',
    },
  },
  quality_flawless_5: {
    code: 'quality_flawless_5',
    category: 'Quality',
    name: {
      en: 'Flawless',
      es: 'Impecable',
    },
    iconEmoji: '🔥',
    ruleDescription: {
      en: 'Master 5 consecutive engrams on the first attempt with high scores.',
      es: 'Domina 5 engrams consecutivos en el primer intento con calificaciones altas.',
    },
    neuroscienceExplanation: {
      en: 'Recognizes sustained cognitive flow state—uninterrupted high-accuracy performance.',
      es: 'Reconoce el estado de flujo cognitivo sostenido—desempeño de alta precisión sin interrupciones.',
    },
    howToEarn: {
      en: 'Maintain a 5-node consecutive streak of first-try high-score passes without any failed attempts in between.',
      es: 'Mantén una racha consecutiva de 5 nodos aprobados a la primera con alta calificación sin reprobaciones intermedias.',
    },
  },

  // CATEGORY E — CHALLENGE
  critical_guardian: {
    code: 'critical_guardian',
    category: 'Challenge',
    name: {
      en: 'Critical Guardian',
      es: 'Guardián Crítico',
    },
    iconEmoji: '🛡️',
    ruleDescription: {
      en: 'Master 10 Critical Tier engrams essential for your role.',
      es: 'Domina 10 engrams de Nivel Crítico esenciales para tu puesto.',
    },
    neuroscienceExplanation: {
      en: 'Directs mental energy and high-effort consolidation to high-impact capabilities that carry the highest operational weight.',
      es: 'Dirige la energía mental y consolidación de alto esfuerzo a capacidades de alto impacto con mayor peso operativo.',
    },
    howToEarn: {
      en: 'Identify and master 10 engrams marked as Critical tier in your job role matrix.',
      es: 'Identifica y domina 10 engrams catalogados con nivel Crítico en la matriz de tu puesto.',
    },
  },

  // CATEGORY F — HABIT & CONSISTENCY
  streak_7: {
    code: 'streak_7',
    category: 'Habit',
    name: {
      en: '7-Day Streak',
      es: 'Racha de 7 Días',
    },
    iconEmoji: '🔥',
    ruleDescription: {
      en: 'Maintain an active daily learning streak for 7 consecutive days.',
      es: 'Mantén una racha activa de aprendizaje diario durante 7 días consecutivos.',
    },
    neuroscienceExplanation: {
      en: 'Establishes the basal ganglia habit loop: daily cue triggering study routine and reward anticipation.',
      es: 'Establece el bucle de hábito en los ganglios basales: disparador diario que activa la rutina de estudio y recompensa.',
    },
    howToEarn: {
      en: 'Study or review at least one engram every day for 7 days in a row.',
      es: 'Estudia o repasa al menos un engram cada día durante 7 días consecutivos.',
    },
  },
  streak_30: {
    code: 'streak_30',
    category: 'Habit',
    name: {
      en: '30-Day Streak',
      es: 'Racha de 30 Días',
    },
    iconEmoji: '🌟',
    ruleDescription: {
      en: 'Maintain an active daily learning streak for 30 consecutive days.',
      es: 'Mantén una racha activa de aprendizaje diario durante 30 días consecutivos.',
    },
    neuroscienceExplanation: {
      en: 'Automaticity threshold. Learning transitions from active willpower to an effortless, default daily ritual.',
      es: 'Umbral de automaticidad. El aprendizaje pasa de requerir fuerza de voluntad activa a ser un ritual diario automático.',
    },
    howToEarn: {
      en: 'Log in and master or review knowledge for 30 uninterrupted days (or protect your streak with freeze days).',
      es: 'Ingresa y domina o repasa conocimientos durante 30 días continuos (o protege tu racha con días de congelamiento).',
    },
  },
  streak_100: {
    code: 'streak_100',
    category: 'Habit',
    name: {
      en: '100-Day Streak',
      es: 'Racha de 100 Días',
    },
    iconEmoji: '💫',
    ruleDescription: {
      en: 'Maintain an active daily learning streak for 100 consecutive days.',
      es: 'Mantén una racha activa de aprendizaje diario durante 100 días consecutivos.',
    },
    neuroscienceExplanation: {
      en: 'Permanent behavioral rewiring. Demonstrates world-class consistency and dedication to continuous self-evolution.',
      es: 'Reconfiguración conductual permanente. Demuestra consistencia de clase mundial y dedicación al crecimiento continuo.',
    },
    howToEarn: {
      en: 'Reach the centennial milestone of 100 days of active learning.',
      es: 'Alcanza el logro centenario de 100 días de aprendizaje activo.',
    },
  },
  automatic_pilot: {
    code: 'automatic_pilot',
    category: 'Habit',
    name: {
      en: 'Autopilot',
      es: 'Piloto Automático',
    },
    iconEmoji: '⚡',
    ruleDescription: {
      en: 'Consolidate 5 engrams into permanent, automatic memory via spaced repetition.',
      es: 'Consolida 5 engrams en memoria permanente y automática mediante repetición espaciada.',
    },
    neuroscienceExplanation: {
      en: 'FSRS spaced retrieval schedules solidify memory traces, bringing stability and retrievability above the permanence threshold.',
      es: 'La programación de recuperación espaciada FSRS solidifica los rastros de memoria, elevando la estabilidad sobre el umbral de permanencia.',
    },
    howToEarn: {
      en: 'Review fading knowledge in "My Brain" until 5 engrams reach permanent automatic retention.',
      es: 'Repasa conocimientos en "Mi Cerebro" hasta que 5 engrams alcancen retención permanente y automática.',
    },
  },
  memory_rescuer: {
    code: 'memory_rescuer',
    category: 'Habit',
    name: {
      en: 'Memory Rescuer',
      es: 'Rescatista de Memoria',
    },
    iconEmoji: '🧠',
    ruleDescription: {
      en: 'Rescue 10 fading engrams when memory retrievability drops below 50%.',
      es: 'Rescata 10 engrams en riesgo de olvido cuando la retención caiga por debajo del 50%.',
    },
    neuroscienceExplanation: {
      en: 'The "testing effect" in action. Retrieving a vulnerable memory trace right before it decays multiplies subsequent memory half-life by up to 4x.',
      es: 'El "efecto de evaluación" en acción. Recuperar un recuerdo vulnerable justo antes de su olvido multiplica su vida media hasta por 4x.',
    },
    howToEarn: {
      en: 'Check "Due for Review" in My Brain and complete reviews on knowledge nodes that are fading.',
      es: 'Revisa la sección de repaso en Mi Cerebro y completa revisiones en nodos cuyo recuerdo esté decayendo.',
    },
  },

  // CATEGORY G — LUCK & SURPRISE
  lucky_jackpot: {
    code: 'lucky_jackpot',
    category: 'Luck',
    name: {
      en: 'Lucky',
      es: 'Afortunado',
    },
    iconEmoji: '🎲',
    ruleDescription: {
      en: 'Hit a rare 3.0x Jackpot reward multiplier upon completing a node.',
      es: 'Obtén un multiplicador sorpresa Jackpot 3.0x al completar un nodo.',
    },
    neuroscienceExplanation: {
      en: 'Leverages B.F. Skinner variable-ratio reinforcement schedules. Unpredictable high rewards trigger maximum dopamine bursts, sustaining long-term engagement.',
      es: 'Aprovecha los programas de refuerzo de razón variable de B.F. Skinner. Recompensas impredecibles detonan picos de dopamina que sostienen el compromiso a largo plazo.',
    },
    howToEarn: {
      en: 'Master knowledge nodes regularly; surprise bonuses trigger randomly on successful completions.',
      es: 'Domina nodos regularmente; las bonificaciones sorpresa se activan de forma aleatoria al completar lecciones con éxito.',
    },
  },
  lucky_triple: {
    code: 'lucky_triple',
    category: 'Luck',
    name: {
      en: 'Triple Luck',
      es: 'Triple Suerte',
    },
    iconEmoji: '🎰',
    ruleDescription: {
      en: 'Hit 3 Jackpot (3.0x) surprise multipliers across your learning journey.',
      es: 'Obtén 3 multiplicadores sorpresa Jackpot (3.0x) a lo largo de tu trayectoria de aprendizaje.',
    },
    neuroscienceExplanation: {
      en: 'Celebrates long-term persistence under variable-ratio reward mechanics.',
      es: 'Celebra la perseverancia a largo plazo bajo mecánicas de recompensa de razón variable.',
    },
    howToEarn: {
      en: 'Continue active study sessions to unlock multiple 3.0x jackpot bonuses over time.',
      es: 'Continúa tus sesiones de estudio para desbloquear múltiples bonificaciones Jackpot 3.0x a lo largo del tiempo.',
    },
  },
};
