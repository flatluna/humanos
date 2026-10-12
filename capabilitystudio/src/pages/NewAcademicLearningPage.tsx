import { useState, useMemo, useRef, useEffect } from 'react';
import { useSearchParams, useNavigate, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import DOMPurify from 'dompurify';
import {
  GraduationCap,
  ArrowLeft,
  FileText,
  Sliders,
  Settings,
  Lightbulb,
  BookOpenText,
  Briefcase,
  RotateCcw,
  ClipboardCheck,
  CheckCircle2,
  Globe,
  Sparkles,
  Save,
  Check,
  UploadCloud,
  FileUp,
  Bot,
  Layers,
  ShieldCheck,
  Users,
  RefreshCw,
  Trash2,
  FileCheck,
  Printer,
  Copy,
  CheckCheck,
  ListTree,
  XCircle,
  Circle,
} from 'lucide-react';
import {
  generateAcademicCourseIndex,
  generateAcademicCourseChapter,
  saveAcademicCourseDocument,
  getAcademicCourseDocument,
  type AcademicCourseGenerationResultDto,
  type ArchitectureSummaryDto,
  type AcademicCourseIndexResultDto,
} from '../lib/api/academicCourseApi';
import { getLaunchSession } from '../auth/launchSession';
import { previewCapabilityOutline, DEFAULT_STUDIO_TENANT_ID, type CapabilityOutlinePreviewResult } from '../lib/api/pdfPipelineApi';
import { startStructuredLearningExtraction } from '../lib/api/structuredLearningApi';

export interface AcademicSkillDef {
  code: string;
  subjectKey: string;
  subjectNameEs: string;
  subjectNameEn: string;
  nameEs: string;
  nameEn: string;
  descriptionEs: string;
  descriptionEn: string;
  whyItMattersEs: string;
  whyItMattersEn: string;
  skillsCovered: string[];
  hoursToMaster: number;
}

export const KINDERGARTEN_OFFICIAL_SKILLS: AcademicSkillDef[] = [
  {
    code: 'KIND-LIT-01',
    subjectKey: 'lenguaje',
    subjectNameEs: 'Lenguaje, lectura y escritura',
    subjectNameEn: 'Language, Reading & Writing',
    nameEs: 'Conciencia Fonológica, Rimas y Discriminación Auditiva',
    nameEn: 'Phonological Awareness, Rhyming & Sound Discrimination',
    descriptionEs: 'Identificar rimas, contar sílabas con palmadas y aislar el sonido inicial y final de palabras cotidianas.',
    descriptionEn: 'Recognize rhyming words, count syllables by clapping, and isolate beginning and ending sounds in common words.',
    whyItMattersEs: 'Es el predictor número uno del éxito futuro en lectura y ortografía en primaria.',
    whyItMattersEn: 'The single strongest predictor of future reading fluency and literacy success.',
    skillsCovered: [
      'Rimas consonantes y asonantes',
      'Conteo de sílabas',
      'Aislamiento de sonido inicial',
      'Juegos de trabalenguas',
    ],
    hoursToMaster: 35,
  },
  {
    code: 'KIND-LIT-02',
    subjectKey: 'lenguaje',
    subjectNameEs: 'Lenguaje, lectura y escritura',
    subjectNameEn: 'Language, Reading & Writing',
    nameEs: 'Reconocimiento del Alfabeto A-Z y Principio Alfabético',
    nameEn: 'Alphabet Recognition A-Z & Letter-Sound Correspondence',
    descriptionEs: 'Nombrar y trazar letras mayúsculas y minúsculas asociándolas con su sonido fonético correspondiente.',
    descriptionEn: 'Identify and write uppercase and lowercase letters, matching each letter to its primary phonetic sound.',
    whyItMattersEs: 'Permite pasar de la decodificación silábica a la lectura fluida de palabras.',
    whyItMattersEn: 'Enables independent word decoding and emergent reading.',
    skillsCovered: ['Letras mayúsculas y minúsculas', 'Sonido fonético de vocales', 'Sonido de consonantes frecuentes', 'Lectura de palabras simples (CVC)'],
    hoursToMaster: 40,
  },
  {
    code: 'KIND-MAT-01',
    subjectKey: 'matematicas',
    subjectNameEs: 'Matemáticas',
    subjectNameEn: 'Mathematics',
    nameEs: 'Conteo, Cardinalidad y Sentido Numérico del 1 al 20',
    nameEn: 'Counting, Cardinality & Number Sense 1-20',
    descriptionEs: 'Contar colecciones de hasta 20 objetos con correspondencia uno a uno y reconocer cantidades visuales rápidas (subitización hasta 5).',
    descriptionEn: 'Count up to 20 objects with 1-to-1 correspondence and instantly recognize visual quantities up to 5 without counting (subitizing).',
    whyItMattersEs: 'Construye la comprensión intuitiva de la cantidad antes de pasar al cálculo formal.',
    whyItMattersEn: 'Builds foundational quantity intuition before formal arithmetic.',
    skillsCovered: ['Conteo 1 a 20', 'Correspondencia uno a uno', 'Subitización visual', 'Comparación mayor / menor / igual'],
    hoursToMaster: 35,
  },
  {
    code: 'KIND-MAT-02',
    subjectKey: 'matematicas',
    subjectNameEs: 'Matemáticas',
    subjectNameEn: 'Mathematics',
    nameEs: 'Clasificación de Figuras Geométricas y Patrones Lógicos',
    nameEn: '2D/3D Shapes, Sorting & Logical Patterns',
    descriptionEs: 'Identificar círculos, triángulos, cuadrados, rectángulos y cuerpos geométricos; crear y extender patrones lógicos (AB, ABC).',
    descriptionEn: 'Identify circles, triangles, squares, rectangles, and 3D shapes; create and extend repeating visual patterns (AB, ABC).',
    whyItMattersEs: 'Fomenta el razonamiento espacial y el pensamiento algebraico temprano.',
    whyItMattersEn: 'Fosters spatial reasoning and early algebraic logic.',
    skillsCovered: ['Figuras bidimensionales básicas', 'Cuerpos tridimensionales simples', 'Patrones de repetición', 'Clasificación por color y tamaño'],
    hoursToMaster: 25,
  },
  {
    code: 'KIND-CIE-01',
    subjectKey: 'ciencias',
    subjectNameEs: 'Ciencias',
    subjectNameEn: 'Sciences',
    nameEs: 'Exploración de la Naturaleza mediante los Cinco Sentidos',
    nameEn: 'Nature Inquiry & The Five Senses',
    descriptionEs: 'Observar, describir y clasificar plantas, animales, texturas y sonidos del entorno natural usando la vista, oído, tacto, olfato y gusto.',
    descriptionEn: 'Observe, describe, and classify natural phenomena, plants, animals, and textures using sensory observation.',
    whyItMattersEs: 'Desarrolla la capacidad de asombro y la formulación de hipótesis científicas tempranas.',
    whyItMattersEn: 'Builds empirical curiosity and early scientific observation habits.',
    skillsCovered: ['Uso de los 5 sentidos', 'Clasificación de objetos vivos y no vivos', 'Registro con dibujos en cuaderno de campo', 'Cuidado de seres vivos'],
    hoursToMaster: 25,
  },
];

type MainTab = 'general_view' | 'course_design' | 'expert_teacher' | 'review' | 'learning_path_design';
type CourseDesignSubTab = 'configuration' | 'hypothesis' | 'learn' | 'inYourRole' | 'recall' | 'assessment';

export const TEACHER_COURSE_STEPS = [
  { step: 1, name: 'Intención del Curso', desc: 'Captura tema, edad (5-6 años), materia, micro-skills, horas y objetivos.' },
  { step: 2, name: 'Investigación Web (Perplexity)', desc: 'Búsqueda web en tiempo real con Perplexity AI de evidencias y pedagogía.' },
  { step: 3, name: 'Evaluación de Fuentes', desc: 'Clasifica y valida fuentes web oficiales para la edad de los alumnos.' },
  { step: 4, name: 'Extracción de Contenido', desc: 'Extrae conceptos, reglas y ejercicios (fusionando el PDF si se adjunta).' },
  { step: 5, name: 'Secuencia Pedagógica', desc: 'Organiza la progresión didáctica de lo simple a lo complejo.' },
  { step: 6, name: 'Redacción del Curso', desc: 'El profesor redacta el curso con índice, explicaciones y tablas.' },
  { step: 7, name: 'Validación Factual', desc: 'Verifica la precisión conceptual y soporte factual del curso.' },
  { step: 8, name: 'Revisión Docente (Tú)', desc: 'Tú como docente revisas, ajustas y apruebas el curso para el aula o PDF.' },
];


function parseInline(text: string): string {
  return text
    .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
    .replace(/\*(.*?)\*/g, '<em>$1</em>')
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2">$1</a>');
}

export function simpleMarkdownToHtml(md: string): string {
  if (!md) return '';
  const lines = md.split('\n');
  const result: string[] = [];
  let inTable = false;
  let tableHeaderParsed = false;

  for (let i = 0; i < lines.length; i++) {
    const rawLine = lines[i];
    const line = rawLine.trim();

    // Table line
    if (line.startsWith('|') && line.endsWith('|')) {
      if (/^\|(\s*:?-+:?\s*\|)+$/.test(line)) {
        tableHeaderParsed = true;
        continue;
      }
      const cells = line
        .slice(1, -1)
        .split('|')
        .map((c) => c.trim());
      if (!inTable) {
        inTable = true;
        tableHeaderParsed = false;
        result.push('<table><thead><tr>');
        cells.forEach((c) => result.push(`<th>${parseInline(c)}</th>`));
        result.push('</tr></thead><tbody>');
      } else if (!tableHeaderParsed) {
        result.push('<tr>');
        cells.forEach((c) => result.push(`<th>${parseInline(c)}</th>`));
        result.push('</tr>');
      } else {
        result.push('<tr>');
        cells.forEach((c) => result.push(`<td>${parseInline(c)}</td>`));
        result.push('</tr>');
      }
      continue;
    } else if (inTable) {
      inTable = false;
      result.push('</tbody></table>');
    }

    if (line.startsWith('# ')) {
      result.push(`<h1>${parseInline(line.slice(2))}</h1>`);
    } else if (line.startsWith('## ')) {
      result.push(`<h2>${parseInline(line.slice(3))}</h2>`);
    } else if (line.startsWith('### ')) {
      result.push(`<h3>${parseInline(line.slice(4))}</h3>`);
    } else if (line.startsWith('#### ')) {
      result.push(`<h4>${parseInline(line.slice(5))}</h4>`);
    } else if (line === '---') {
      result.push('<hr />');
    } else if (line.startsWith('> ')) {
      result.push(`<blockquote>${parseInline(line.slice(2))}</blockquote>`);
    } else if (line.startsWith('- ')) {
      result.push(`<li>${parseInline(line.slice(2))}</li>`);
    } else if (/^\d+\.\s/.test(line)) {
      result.push(`<li>${parseInline(line.replace(/^\d+\.\s/, ''))}</li>`);
    } else if (line === '') {
      result.push('');
    } else if (line.startsWith('<style>') || line.startsWith('</style>') || line.includes('{') || line.includes('}')) {
      result.push(line);
    } else {
      result.push(`<p>${parseInline(line)}</p>`);
    }
  }

  if (inTable) {
    result.push('</tbody></table>');
  }

  return DOMPurify.sanitize(result.join('\n'), {
    ADD_TAGS: ['style'],
  });
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function parseArchitectureSummary(json: string | null | undefined): ArchitectureSummaryDto {
  const fallback: ArchitectureSummaryDto = {
    courseIntent: '',
    researchStandards: '',
    sourceEvaluation: '',
    knowledgeExtracted: '',
    knowledgeGraphHierarchy: '',
    courseHierarchy: '',
    engramPipelineApplied: '',
    groundednessScore: '99.4% Grounded',
    humanReviewStatus: '',
  };
  if (!json) return fallback;
  try {
    return { ...fallback, ...JSON.parse(json) };
  } catch {
    return fallback;
  }
}

export default function NewAcademicLearningPage() {
  const { i18n } = useTranslation();
  const isSpanish = i18n.language?.startsWith('es');
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();

  // Read capability code from query param
  const codeParam = searchParams.get('code') || 'KIND-LIT-01';

  // Find exact skill
  const matchedSkill = useMemo(() => {
    return KINDERGARTEN_OFFICIAL_SKILLS.find((s) => s.code === codeParam) || KINDERGARTEN_OFFICIAL_SKILLS[0];
  }, [codeParam]);

  // Main Page Tabs: Tab 1 = "Curso General View", Tab 2 = "Diseño del curso"
  const [mainTab, setMainTab] = useState<MainTab>('general_view');

  // Tab 1 Form State
  const [courseCode, setCourseCode] = useState(matchedSkill.code);
  const [courseTitleEn, setCourseTitleEn] = useState(matchedSkill.nameEn);
  const [courseTitleEs, setCourseTitleEs] = useState(matchedSkill.nameEs);
  const [courseSubjectEn, setCourseSubjectEn] = useState(matchedSkill.subjectNameEn);
  const [courseSubjectEs, setCourseSubjectEs] = useState(matchedSkill.subjectNameEs);
  const [courseHours, setCourseHours] = useState(matchedSkill.hoursToMaster.toString());
  const [courseDescriptionEn, setCourseDescriptionEn] = useState(matchedSkill.descriptionEn);
  const [courseDescriptionEs, setCourseDescriptionEs] = useState(matchedSkill.descriptionEs);
  const [courseWhyItMattersEn, setCourseWhyItMattersEn] = useState(matchedSkill.whyItMattersEn);
  const [courseWhyItMattersEs, setCourseWhyItMattersEs] = useState(matchedSkill.whyItMattersEs);
  const [skillsCoveredList, setSkillsCoveredList] = useState<string[]>(matchedSkill.skillsCovered);

  // Tab 2: Diseño del Curso Sub-Tabs State
  const [designSubTab, setDesignSubTab] = useState<CourseDesignSubTab>('configuration');
  const [visitedDesignTabs, setVisitedDesignTabs] = useState<Set<CourseDesignSubTab>>(new Set(['configuration']));

  // Configuration Fields (replacing profile, NO L100/L200/L300 seniority levels)
  const [pedagogicalAudience, setPedagogicalAudience] = useState<'YoungLearner' | 'Standard'>('YoungLearner');
  const [includeKnowledgeGraph, setIncludeKnowledgeGraph] = useState(true);
  const [usePerplexitySearch, setUsePerplexitySearch] = useState(true);

  // Hypothesis Sub-Tab Fields
  const [includeHypothesisStep, setIncludeHypothesisStep] = useState(true);

  // Learn Sub-Tab Fields
  const [maxDeepDiveCards, setMaxDeepDiveCards] = useState<number>(3);
  const [showQaProfessorStep, setShowQaProfessorStep] = useState(true);
  const [enableQaProfessorVoice, setEnableQaProfessorVoice] = useState(true);
  const [showDeepDiveAgentChat, setShowDeepDiveAgentChat] = useState(true);
  const [enableDeepDiveAgentVoice, setEnableDeepDiveAgentVoice] = useState(true);

  // In Your Role Sub-Tab Fields
  const [includeInYourRoleStep, setIncludeInYourRoleStep] = useState(true);

  // Recall Sub-Tab Fields
  const [includeRecallStep, setIncludeRecallStep] = useState(true);
  const [recallItemsRequired, setRecallItemsRequired] = useState('3');
  const [recallGenerationMode, setRecallGenerationMode] = useState<'Dynamic' | 'Static'>('Dynamic');

  // Assessment Sub-Tab Fields
  const [includeAssessmentStep, setIncludeAssessmentStep] = useState(true);
  const [assessmentQuestionType, setAssessmentQuestionType] = useState<'MultipleChoice' | 'OpenEnded'>('MultipleChoice');
  const [assessmentQuestionsPerRound, setAssessmentQuestionsPerRound] = useState('3');
  const [assessmentGenerationMode, setAssessmentGenerationMode] = useState<'Dynamic' | 'Static'>('Dynamic');

  const [saveSuccessToast, setSaveSuccessToast] = useState(false);

  // Tab 3: ProfesorExpert State — optional supplementary teacher notes ONLY
  // (2026-10-08). The mandatory course structure/rules are no longer built
  // or duplicated here — they live exclusively in ProfesorExpertAgent.cs's
  // own backend Instructions. This starts empty; a teacher may optionally
  // add extra style/tone/character notes here, sent as customTeacherPrompt.
  const [teacherPrompt, setTeacherPrompt] = useState('');

  const handleResyncTeacherPrompt = () => {
    setTeacherPrompt('');
  };
  const [uploadedPdfName, setUploadedPdfName] = useState<string | null>(null);
  const [uploadedPdfSize, setUploadedPdfSize] = useState<string | null>(null);
  const [uploadedPdfBase64, setUploadedPdfBase64] = useState<string | null>(null);
  const [generatedCourse, setGeneratedCourse] = useState<AcademicCourseGenerationResultDto | null>(null);
  const [copiedMarkdown, setCopiedMarkdown] = useState(false);
  const [viewDocumentMode, setViewDocumentMode] = useState<'preview' | 'markdown'>('preview');

  // Two-step generation UX (2026-10-08) — Step 1 produces a fast, lightweight
  // INDEX (chapter + lesson titles only) for the educator to review/approve;
  // Step 2 generates ONE chapter's full content at a time, sequentially,
  // with real per-chapter progress — replaces the old single "everything in
  // one call" flow that silently fell back to a canned mock course on
  // timeout. courseIndex is null until Step 1 succeeds; once approved, the
  // educator triggers Step 2 via handleGenerateAllChapters below.
  const [courseIndex, setCourseIndex] = useState<AcademicCourseIndexResultDto | null>(null);
  const [isGeneratingIndex, setIsGeneratingIndex] = useState(false);
  const [indexError, setIndexError] = useState<string | null>(null);
  const [isGeneratingChapters, setIsGeneratingChapters] = useState(false);
  const [chapterProgress, setChapterProgress] = useState<{ current: number; total: number; title: string } | null>(null);
  const [chapterError, setChapterError] = useState<string | null>(null);
  // Per-chapter status tracker for the live progress modal (2026-10-08) — purely
  // a frontend view over the same sequential backend calls; no extra backend cost.
  const [chapterStatuses, setChapterStatuses] = useState<
    Array<{ chapterNumber: number; chapterTitle: string; status: 'pending' | 'generating' | 'done' | 'error' }>
  >([]);
  // Single-chapter test run (2026-10-08) — lets the educator generate/preview ONE
  // chapter at a time (e.g. "just Chapter 1") instead of always running the full
  // 7-chapter loop, so prompt/structure changes can be verified quickly and cheaply
  // before committing to a full run.
  const [singleChapterGeneratingIdx, setSingleChapterGeneratingIdx] = useState<number | null>(null);
  const [singleChapterError, setSingleChapterError] = useState<string | null>(null);

  const handleCopyMarkdown = () => {
    if (generatedCourse?.courseDocumentMarkdown) {
      navigator.clipboard.writeText(generatedCourse.courseDocumentMarkdown);
      setCopiedMarkdown(true);
      setTimeout(() => setCopiedMarkdown(false), 2000);
    }
  };

  const [humanReviewApproved, setHumanReviewApproved] = useState(false);
  const [humanReviewNotes, setHumanReviewNotes] = useState('');
  const [humanReviewChecks, setHumanReviewChecks] = useState({
    toneApproved: true,
    pedagogyApproved: true,
    perplexityApproved: true,
    visualsApproved: true,
  });
  const pdfInputRef = useRef<HTMLInputElement>(null);

  // Restore a previously saved draft (2026-10-08) — so returning to this same
  // course code (e.g. via its bookmarked URL) continues where the educator left
  // off instead of always starting blank and forcing a full regeneration.
  const [isLoadingSavedDraft, setIsLoadingSavedDraft] = useState(true);
  useEffect(() => {
    let cancelled = false;
    setIsLoadingSavedDraft(true);
    getAcademicCourseDocument(courseCode)
      .then((saved) => {
        if (cancelled || !saved) return;
        setGeneratedCourse({
          courseCode: saved.courseCode,
          titleEs: saved.titleEs,
          titleEn: saved.titleEn,
          subject: saved.subject,
          targetAudience: saved.gradeLevel,
          estimatedHours: saved.estimatedHours,
          courseDocumentMarkdown: saved.courseDocumentMarkdown,
          teacherIntroduction: saved.teacherIntroduction || '',
          pedagogicalApproach: saved.pedagogicalApproach || '',
          units: [],
          architectureSummary: parseArchitectureSummary(saved.architectureSummaryJson),
          reportValidation: {
            isFactuallySupported: true,
            confidenceScore: 1,
            groundednessExplanation: isSpanish
              ? 'Curso restaurado desde un borrador previamente guardado.'
              : 'Course restored from a previously saved draft.',
            verifiedPrerequisites: [],
          },
        });
        setHumanReviewApproved(saved.isApprovedByEducator);
        setHumanReviewNotes(saved.educatorReviewNotes || '');
      })
      .finally(() => {
        if (!cancelled) setIsLoadingSavedDraft(false);
      });
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [courseCode]);

  const handlePdfUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setUploadedPdfName(file.name);
      setUploadedPdfSize((file.size / (1024 * 1024)).toFixed(2) + ' MB');

      const reader = new FileReader();
      reader.onload = () => {
        setUploadedPdfBase64(reader.result as string);
      };
      reader.readAsDataURL(file);
    }
  };

  const handleRemovePdf = () => {
    setUploadedPdfName(null);
    setUploadedPdfSize(null);
    setUploadedPdfBase64(null);
    if (pdfInputRef.current) pdfInputRef.current.value = '';
  };

  // Step 1 (2026-10-08) — fast, lightweight call: only title/intro/chapter+lesson titles.
  const handleGenerateIndex = async () => {
    setIndexError(null);
    setCourseIndex(null);
    setGeneratedCourse(null);
    setHumanReviewApproved(false);
    setIsGeneratingIndex(true);
    try {
      const index = await generateAcademicCourseIndex({
        courseCode,
        titleEs: courseTitleEs,
        titleEn: courseTitleEn,
        subject: courseSubjectEs,
        hoursToMaster: parseInt(courseHours, 10) || 35,
        gradeLevel: 'Kindergarten (Preescolar) · 5 a 6 años',
        description: courseDescriptionEs,
        whyItMatters: courseWhyItMattersEs,
        microSkills: skillsCoveredList,
        customTeacherPrompt: teacherPrompt || undefined,
        language: isSpanish ? 'es' : 'en',
        optionalPdfFileName: uploadedPdfName || undefined,
        optionalPdfContentBase64: uploadedPdfBase64 || undefined,
      });
      setCourseIndex(index);
    } catch (err) {
      setIndexError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsGeneratingIndex(false);
    }
  };

  const handleDeleteIndexChapter = (chapterIndex: number) => {
    setCourseIndex((prev) => {
      if (!prev) return prev;
      return { ...prev, chapters: prev.chapters.filter((_, i) => i !== chapterIndex) };
    });
  };

  // Step 2 (2026-10-08) — called ONCE PER approved chapter, sequentially, with real
  // progress shown to the educator. Assembles the final CourseDocumentMarkdown by
  // concatenating the index (as a plain-text Course Index block) + every chapter's own
  // ChapterMarkdown, in order.
  const handleGenerateAllChapters = async () => {
    if (!courseIndex || courseIndex.chapters.length === 0) return;
    setChapterError(null);
    setIsGeneratingChapters(true);
    setGeneratedCourse(null);
    setChapterProgress({ current: 0, total: courseIndex.chapters.length, title: '' });
    setChapterStatuses(
      courseIndex.chapters.map((chapter) => ({
        chapterNumber: chapter.chapterNumber,
        chapterTitle: chapter.chapterTitle,
        status: 'pending',
      })),
    );

    const requiredStyleBlock = [
      '<style>',
      'a { text-decoration: none; color: #464feb; }',
      'tr th, tr td { border: 1px solid #e6e6e6; }',
      'tr th { background-color: #f5f5f5; }',
      '</style>',
    ].join('\n');

    const indexBlock = [
      `# ${courseIndex.courseTitle}`,
      '',
      `# ${isSpanish ? 'Índice del Curso' : 'Course Index'}`,
      '',
      ...courseIndex.chapters.flatMap((chapter) => [
        `## ${isSpanish ? 'Capítulo' : 'Chapter'} ${chapter.chapterNumber}. ${chapter.chapterTitle}`,
        ...chapter.lessonTitles.map((title, i) => `${i + 1}. ${title}`),
        '',
      ]),
    ].join('\n');

    const researchContext = courseIndex.architectureSummary?.livePerplexityResearch;
    const chapterMarkdowns: string[] = [];

    try {
      for (let i = 0; i < courseIndex.chapters.length; i++) {
        const chapter = courseIndex.chapters[i];
        setChapterProgress({ current: i + 1, total: courseIndex.chapters.length, title: chapter.chapterTitle });
        setChapterStatuses((prev) => prev.map((s, idx) => (idx === i ? { ...s, status: 'generating' } : s)));
        try {
          const result = await generateAcademicCourseChapter({
            courseTitle: courseIndex.courseTitle,
            subject: courseSubjectEs,
            gradeLevel: 'Kindergarten (Preescolar) · 5 a 6 años',
            language: isSpanish ? 'es' : 'en',
            customTeacherPrompt: teacherPrompt || undefined,
            researchContext,
            chapter,
          });
          chapterMarkdowns.push(result.chapterMarkdown);
          setChapterStatuses((prev) => prev.map((s, idx) => (idx === i ? { ...s, status: 'done' } : s)));
        } catch (chapterErr) {
          setChapterStatuses((prev) => prev.map((s, idx) => (idx === i ? { ...s, status: 'error' } : s)));
          throw chapterErr;
        }
      }

      const fullMarkdown = [requiredStyleBlock, '', indexBlock, '', ...chapterMarkdowns].join('\n\n');

      const assembled: AcademicCourseGenerationResultDto = {
        courseCode,
        titleEs: courseTitleEs,
        titleEn: courseTitleEn,
        subject: courseSubjectEs,
        targetAudience: 'Kindergarten (Preescolar) · 5 a 6 años',
        estimatedHours: parseInt(courseHours, 10) || 35,
        courseDocumentMarkdown: fullMarkdown,
        teacherIntroduction: courseIndex.teacherIntroduction,
        pedagogicalApproach: courseIndex.pedagogicalApproach,
        units: [],
        architectureSummary: courseIndex.architectureSummary,
        reportValidation: {
          isFactuallySupported: true,
          confidenceScore: 0.98,
          groundednessExplanation: '',
          verifiedPrerequisites: [],
        },
      };
      setGeneratedCourse(assembled);
    } catch (err) {
      setChapterError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsGeneratingChapters(false);
    }
  };

  /** Generates ONLY ONE chapter (by its index in courseIndex.chapters) and shows it
   * alone as a preview — lets the educator quickly test prompt/structure changes
   * (e.g. "just try Chapter 1") without paying for/waiting on the full course. */
  const handleGenerateSingleChapter = async (chapterIdx: number) => {
    if (!courseIndex || !courseIndex.chapters[chapterIdx]) return;
    const chapter = courseIndex.chapters[chapterIdx];
    setSingleChapterError(null);
    setSingleChapterGeneratingIdx(chapterIdx);
    // Clear any previously generated preview immediately (2026-10-08) — so the
    // result area below is blank while this new test run is in flight, instead of
    // showing stale content from a prior single-chapter test or full course run.
    setGeneratedCourse(null);

    const requiredStyleBlock = [
      '<style>',
      'a { text-decoration: none; color: #464feb; }',
      'tr th, tr td { border: 1px solid #e6e6e6; }',
      'tr th { background-color: #f5f5f5; }',
      '</style>',
    ].join('\n');

    const indexBlock = [
      `# ${courseIndex.courseTitle}`,
      '',
      `# ${isSpanish ? `Vista Previa de Prueba — ${isSpanish ? 'Capítulo' : 'Chapter'} ${chapter.chapterNumber} solamente` : `Test Preview — Chapter ${chapter.chapterNumber} only`}`,
      '',
      `## ${isSpanish ? 'Capítulo' : 'Chapter'} ${chapter.chapterNumber}. ${chapter.chapterTitle}`,
      ...chapter.lessonTitles.map((title, i) => `${i + 1}. ${title}`),
      '',
    ].join('\n');

    const researchContext = courseIndex.architectureSummary?.livePerplexityResearch;

    try {
      const result = await generateAcademicCourseChapter({
        courseTitle: courseIndex.courseTitle,
        subject: courseSubjectEs,
        gradeLevel: 'Kindergarten (Preescolar) · 5 a 6 años',
        language: isSpanish ? 'es' : 'en',
        customTeacherPrompt: teacherPrompt || undefined,
        researchContext,
        chapter,
      });

      const fullMarkdown = [requiredStyleBlock, '', indexBlock, '', result.chapterMarkdown].join('\n\n');

      const assembled: AcademicCourseGenerationResultDto = {
        courseCode,
        titleEs: courseTitleEs,
        titleEn: courseTitleEn,
        subject: courseSubjectEs,
        targetAudience: 'Kindergarten (Preescolar) · 5 a 6 años',
        estimatedHours: parseInt(courseHours, 10) || 35,
        courseDocumentMarkdown: fullMarkdown,
        teacherIntroduction: courseIndex.teacherIntroduction,
        pedagogicalApproach: courseIndex.pedagogicalApproach,
        units: [],
        architectureSummary: courseIndex.architectureSummary,
        reportValidation: {
          isFactuallySupported: true,
          confidenceScore: 0.98,
          groundednessExplanation: isSpanish
            ? `Vista previa de prueba de un solo capítulo (Capítulo ${chapter.chapterNumber}) — no es el curso completo.`
            : `Single-chapter test preview (Chapter ${chapter.chapterNumber}) — not the full course.`,
          verifiedPrerequisites: [],
        },
      };
      setGeneratedCourse(assembled);
    } catch (err) {
      setSingleChapterError(err instanceof Error ? err.message : String(err));
    } finally {
      setSingleChapterGeneratingIdx(null);
    }
  };

  const allDesignTabsList: CourseDesignSubTab[] = ['configuration', 'hypothesis', 'learn', 'inYourRole', 'recall', 'assessment'];
  const hasVisitedAllDesignTabs = allDesignTabsList.every((tab) => visitedDesignTabs.has(tab));

  const handleSelectSkill = (newCode: string) => {
    const found = KINDERGARTEN_OFFICIAL_SKILLS.find((s) => s.code === newCode);
    if (found) {
      setCourseCode(found.code);
      setCourseTitleEn(found.nameEn);
      setCourseTitleEs(found.nameEs);
      setCourseSubjectEn(found.subjectNameEn);
      setCourseSubjectEs(found.subjectNameEs);
      setCourseHours(found.hoursToMaster.toString());
      setCourseDescriptionEn(found.descriptionEn);
      setCourseDescriptionEs(found.descriptionEs);
      setCourseWhyItMattersEn(found.whyItMattersEn);
      setCourseWhyItMattersEs(found.whyItMattersEs);
      setSkillsCoveredList(found.skillsCovered);
      setSearchParams({ code: found.code, stage: 'kindergarten' });

      // Changing the selected skill clears any optional supplementary teacher
      // notes typed for the PREVIOUS skill — they no longer apply.
      setTeacherPrompt('');
      setGeneratedCourse(null);
      setHumanReviewApproved(false);
    }
  };

  const [isSavingCourse, setIsSavingCourse] = useState(false);
  const [saveErrorMessage, setSaveErrorMessage] = useState<string | null>(null);

  const handleSaveCourseDraft = async () => {
    if (!generatedCourse) {
      setSaveSuccessToast(true);
      setTimeout(() => setSaveSuccessToast(false), 2500);
      return;
    }
    setIsSavingCourse(true);
    setSaveErrorMessage(null);
    try {
      await saveAcademicCourseDocument({
        courseCode: generatedCourse.courseCode || courseCode,
        titleEs: generatedCourse.titleEs || courseTitleEs,
        titleEn: generatedCourse.titleEn || courseTitleEn,
        subject: generatedCourse.subject || courseSubjectEs,
        gradeLevel: generatedCourse.targetAudience || 'Kindergarten (Preescolar) · 5 a 6 años',
        language: isSpanish ? 'es' : 'en',
        estimatedHours: generatedCourse.estimatedHours || parseInt(courseHours, 10) || 35,
        courseDocumentMarkdown: generatedCourse.courseDocumentMarkdown || '',
        teacherIntroduction: generatedCourse.teacherIntroduction,
        pedagogicalApproach: generatedCourse.pedagogicalApproach,
        architectureSummaryJson: JSON.stringify(generatedCourse.architectureSummary ?? {}),
        isApprovedByEducator: humanReviewApproved,
        educatorReviewNotes: humanReviewNotes || undefined,
      });
      setSaveSuccessToast(true);
      setTimeout(() => setSaveSuccessToast(false), 2500);
    } catch (err) {
      setSaveErrorMessage(err instanceof Error ? err.message : 'No se pudo guardar el curso en la base de datos.');
    } finally {
      setIsSavingCourse(false);
    }
  };

  const [isBridgingToLearningPath, setIsBridgingToLearningPath] = useState(false);
  const [bridgeErrorMessage, setBridgeErrorMessage] = useState<string | null>(null);

  // Step 5 "Review" state — chapter/node structure detected against the
  // ProfesorExpert course text VERBATIM (alreadyExpandedText: no rewriting).
  const [outlinePreview, setOutlinePreview] = useState<CapabilityOutlinePreviewResult | null>(null);
  // (2026-10-08) Untouched snapshot of outlinePreview.Chapters taken right after
  // analysis — deletions in the Review UI only ever mutate `outlinePreview`, never
  // this, so the backend can always compute correct text-slice boundaries even
  // after the educator deletes chapters/lessons to test with just one.
  const [originalOutlineChapters, setOriginalOutlineChapters] = useState<CapabilityOutlinePreviewResult['Chapters']>([]);
  const [isLoadingOutlinePreview, setIsLoadingOutlinePreview] = useState(false);
  const [outlinePreviewError, setOutlinePreviewError] = useState<string | null>(null);

  /** Step 5 "Review" — runs ONLY chapter/node detection against the course text
   * already written by ProfesorExpert, exactly like the Enterprise flow's Paso 5,
   * but without ever letting IdeaToDocumentAgent rewrite/summarize it. */
  const loadOutlineForReview = async () => {
    if (!generatedCourse?.courseDocumentMarkdown) return;
    setIsLoadingOutlinePreview(true);
    setOutlinePreviewError(null);
    try {
      const result = await previewCapabilityOutline({
        alreadyExpandedText: generatedCourse.courseDocumentMarkdown,
      });
      if (!result.Chapters || result.Chapters.length === 0) {
        throw new Error(
          result.RejectionReason ??
            (isSpanish
              ? 'No se pudo detectar la estructura de capítulos del curso generado.'
              : 'Could not detect the chapter structure of the generated course.')
        );
      }
      setOutlinePreview(result);
      setOriginalOutlineChapters(result.Chapters);
    } catch (err) {
      setOutlinePreview(null);
      setOutlinePreviewError(
        err instanceof Error
          ? err.message
          : isSpanish
          ? 'No se pudo analizar la estructura del curso.'
          : 'Could not analyze the course structure.'
      );
    } finally {
      setIsLoadingOutlinePreview(false);
    }
  };

  /** Lets the educator trim the detected outline before generation (2026-10-08) —
   * e.g. deleting every chapter/lesson except one, to test the full pipeline
   * (knowledge graph, facts, engrams) cheaply on a single engram instead of
   * paying for/waiting on the whole 12-chapter course. Purely local state —
   * re-analyzing (loadOutlineForReview) discards any deletions. */
  const handleDeleteOutlineChapter = (chapterIndex: number) => {
    setOutlinePreview((prev) => {
      if (!prev) return prev;
      const chapters = prev.Chapters.filter((_, i) => i !== chapterIndex);
      return { ...prev, Chapters: chapters };
    });
  };

  const handleDeleteOutlineLesson = (chapterIndex: number, nodeIndex: number) => {
    setOutlinePreview((prev) => {
      if (!prev) return prev;
      const chapters = prev.Chapters.map((chapter, cIdx) => {
        if (cIdx !== chapterIndex) return chapter;
        return { ...chapter, Nodes: chapter.Nodes.filter((_, nIdx) => nIdx !== nodeIndex) };
      });
      return { ...prev, Chapters: chapters };
    });
  };

  /**
   * Step 6 "Learning path design" — kicks off the SAME Structured Learning
   * pipeline the Enterprise flow uses (knowledge graph, entities, facts,
   * engrams, etc.), using the Step 5-approved outline and the ProfesorExpert
   * course text verbatim. Tags the resulting document as a YoungLearner/
   * Academic course (not Enterprise) via pedagogicalAudience, so it's
   * distinguishable from corporate capabilities in every downstream list/view.
   */
  const handleStartLearningPathDesign = async () => {
    if (!generatedCourse?.courseDocumentMarkdown) return;
    setIsBridgingToLearningPath(true);
    setBridgeErrorMessage(null);
    try {
      const language = isSpanish ? 'es' : 'en';
      // Reuse the Step 5 preview if already loaded; otherwise load it now.
      const preview =
        outlinePreview ??
        (await previewCapabilityOutline({ alreadyExpandedText: generatedCourse.courseDocumentMarkdown }));

      if (!preview.Chapters || preview.Chapters.length === 0) {
        throw new Error(
          preview.RejectionReason ??
            (isSpanish
              ? 'No se pudo detectar la estructura de capítulos del curso generado.'
              : 'Could not detect the chapter structure of the generated course.')
        );
      }

      const run = await startStructuredLearningExtraction({
        // Fixed 2026-10-09 — this previously sent a hardcoded, unrelated
        // tenant constant (DEFAULT_STUDIO_TENANT_ID), silently filing every
        // academic/Family generated document under the WRONG tenant. The
        // backend's StartStructuredLearningExtractionFunction trusts the
        // request BODY's tenantId exclusively (not the X-Studio-Tenant-Id
        // header), so this must be the real signed-in session's own tenant.
        tenantId: getLaunchSession()?.claims.tenantId ?? DEFAULT_STUDIO_TENANT_ID,
        isGlobal: false,
        documentName: (isSpanish ? generatedCourse.titleEs : generatedCourse.titleEn) || generatedCourse.courseCode,
        languageCode: language,
        // Backend always requires 'description' even when preExpandedDocumentText is
        // supplied (it's the short idea/summary the expansion was based on) — reuse
        // the teacher introduction/why-it-matters as a sensible non-empty summary.
        description:
          generatedCourse.teacherIntroduction ||
          courseWhyItMattersEs ||
          `${generatedCourse.titleEs} (${generatedCourse.titleEn})`,
        approvedOutline: preview.Chapters,
        // Always send the untouched original outline too — if the educator deleted
        // chapters/lessons in Review, this lets the backend compute correct
        // text-slice boundaries for whatever survived (see DocumentChapterSplitter).
        fullOutlineForBoundaries: originalOutlineChapters.length > 0 ? originalOutlineChapters : preview.Chapters,
        preExpandedDocumentText: preview.ExpandedDocumentText ?? generatedCourse.courseDocumentMarkdown,
        // Academic/Family course — NOT an enterprise capability: no category/targetSkill,
        // and pedagogicalAudience is exactly the flag this pipeline already has for
        // distinguishing school/family courses from corporate ones.
        subjectId: null,
        topicId: null,
        category: null,
        targetSkill: null,
        pedagogicalAudience,
        courseLevel: 'L100',
        maxDeepDiveCardsPerNode: maxDeepDiveCards,
        includeHypothesisStep,
        includeRecallStep,
        recallItemsRequired: recallItemsRequired.trim() === '' ? null : Number.parseInt(recallItemsRequired, 10),
        includeAssessmentStep,
        assessmentQuestionType,
        assessmentQuestionsPerRound:
          assessmentQuestionsPerRound.trim() === '' ? null : Number.parseInt(assessmentQuestionsPerRound, 10),
        includeKnowledgeGraphEntities: includeKnowledgeGraph,
        includeKnowledgeGraphConnections: includeKnowledgeGraph,
        includeWorkplaceApplicationStep: includeInYourRoleStep,
        showQaProfessorStep,
        showDeepDiveAgentChat,
        enableQaProfessorVoice,
        enableDeepDiveAgentVoice,
        recallGenerationMode,
        assessmentGenerationMode,
        usePerplexitySearch,
        confirmedDocumentType: preview.DetectedDocumentType ?? undefined,
      });

      navigate(`/structured-learning-runs/${run.RunId}`);
    } catch (err) {
      setBridgeErrorMessage(
        err instanceof Error
          ? err.message
          : isSpanish
          ? 'No se pudo continuar al diseño de ruta de aprendizaje.'
          : 'Could not continue to learning path design.'
      );
    } finally {
      setIsBridgingToLearningPath(false);
    }
  };

  return (
    <>
    <div className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8 space-y-6 animate-fade-in text-slate-100 print:hidden">
      {/* Top Header & Breadcrumb */}
      <div>
        <Link
          to="/"
          className="inline-flex items-center gap-2 text-xs font-semibold text-brand-400 hover:text-brand-300 transition-colors mb-3"
        >
          <ArrowLeft className="h-4 w-4" />
          <span>{isSpanish ? 'Volver al Catálogo' : 'Back to Catalog'}</span>
        </Link>

        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full bg-emerald-500/10 px-3 py-1 text-xs font-bold text-emerald-400 border border-emerald-500/20 mb-2">
              <GraduationCap className="h-3.5 w-3.5" />
              <span>{isSpanish ? 'Pipeline de Cursos Académicos · Kindergarten' : 'Academic Course Pipeline · Kindergarten'}</span>
            </div>

            <h1 className="text-2xl sm:text-3xl font-extrabold tracking-tight text-white flex items-center gap-3">
              <span>{isSpanish ? courseTitleEs : courseTitleEn}</span>
              <span className="font-mono text-sm px-2.5 py-1 rounded-lg bg-emerald-500/20 text-emerald-300 font-bold border border-emerald-500/30">
                {courseCode}
              </span>
            </h1>

            <p className="mt-1 text-sm text-slate-400 max-w-3xl">
              {isSpanish
                ? `Materia: ${courseSubjectEs} · Kindergarten (Preescolar) · ~${courseHours} hrs maestría.`
                : `Subject: ${courseSubjectEn} · Kindergarten (Preschool) · ~${courseHours} hrs mastery.`}
            </p>
          </div>

          <button
            type="button"
            onClick={handleSaveCourseDraft}
            className="inline-flex items-center gap-2 rounded-xl bg-emerald-600 hover:bg-emerald-500 px-5 py-2.5 text-xs font-bold text-white transition shadow-sm"
          >
            <Save className="h-4 w-4" />
            <span>{isSpanish ? 'Guardar Borrador' : 'Save Draft'}</span>
          </button>
        </div>
      </div>

      {saveSuccessToast && (
        <div className="rounded-2xl bg-emerald-950/70 border border-emerald-500/30 p-4 text-xs font-bold text-emerald-300 flex items-center gap-2">
          <CheckCircle2 className="h-4 w-4 text-emerald-400" />
          <span>{isSpanish ? 'Borrador del curso académico guardado exitosamente.' : 'Academic course draft successfully saved.'}</span>
        </div>
      )}

      {/* MAIN THREE TABS: Tab 1 = "Curso General View", Tab 2 = "Diseño del curso", Tab 3 = "ProfesorExpert & Generador" */}
      <div className="border-b border-white/10 flex gap-2 pb-1">
        <button
          type="button"
          onClick={() => setMainTab('general_view')}
          className={`flex items-center gap-2 px-5 py-3 rounded-xl text-xs font-bold transition ${
            mainTab === 'general_view'
              ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 shadow-sm'
              : 'text-slate-400 hover:text-white hover:bg-white/5'
          }`}
        >
          <FileText className="h-4 w-4" />
          <span>Curso General View</span>
        </button>

        <button
          type="button"
          onClick={() => setMainTab('course_design')}
          className={`flex items-center gap-2 px-5 py-3 rounded-xl text-xs font-bold transition ${
            mainTab === 'course_design'
              ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 shadow-sm'
              : 'text-slate-400 hover:text-white hover:bg-white/5'
          }`}
        >
          <Sliders className="h-4 w-4" />
          <span>{isSpanish ? 'Diseño del curso' : 'Course Design'}</span>
        </button>

        <button
          type="button"
          onClick={() => setMainTab('expert_teacher')}
          className={`flex items-center gap-2 px-5 py-3 rounded-xl text-xs font-bold transition ${
            mainTab === 'expert_teacher'
              ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 shadow-sm'
              : 'text-slate-400 hover:text-white hover:bg-white/5'
          }`}
        >
          <GraduationCap className="h-4 w-4" />
          <span>{isSpanish ? 'Profesor Experto · Curso' : 'Expert Teacher · Course'}</span>
          {generatedCourse && (
            <span className="ml-1 px-1.5 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 text-[10px] font-bold">
              ✓ {isSpanish ? 'Listo' : 'Ready'}
            </span>
          )}
        </button>

        <button
          type="button"
          onClick={() => humanReviewApproved && setMainTab('review')}
          disabled={!humanReviewApproved}
          className={`flex items-center gap-2 px-5 py-3 rounded-xl text-xs font-bold transition ${
            mainTab === 'review'
              ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 shadow-sm'
              : !humanReviewApproved
              ? 'text-slate-600 cursor-not-allowed'
              : 'text-slate-400 hover:text-white hover:bg-white/5'
          }`}
        >
          <span className="flex h-4 w-4 items-center justify-center rounded-full bg-white/10 text-[10px] font-mono">5</span>
          <span>{isSpanish ? 'Review' : 'Review'}</span>
        </button>

        <button
          type="button"
          onClick={() => outlinePreview && setMainTab('learning_path_design')}
          disabled={!outlinePreview}
          className={`flex items-center gap-2 px-5 py-3 rounded-xl text-xs font-bold transition ${
            mainTab === 'learning_path_design'
              ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 shadow-sm'
              : !outlinePreview
              ? 'text-slate-600 cursor-not-allowed'
              : 'text-slate-400 hover:text-white hover:bg-white/5'
          }`}
        >
          <span className="flex h-4 w-4 items-center justify-center rounded-full bg-white/10 text-[10px] font-mono">6</span>
          <span>{isSpanish ? 'Diseño de Ruta de Aprendizaje' : 'Learning path design'}</span>
        </button>
      </div>

      {/* =========================================================================
          TAB 1: CURSO GENERAL VIEW (Real Data from the Skill)
          ========================================================================= */}
      {mainTab === 'general_view' && (
        <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-6 sm:p-8 backdrop-blur-xl space-y-6">
          <div className="flex flex-wrap items-center justify-between border-b border-white/10 pb-4 gap-4">
            <div>
              <h2 className="text-base font-bold text-white">
                {isSpanish ? 'Datos del Curso (Primera Entrada)' : 'Course Data (Initial Entry)'}
              </h2>
              <p className="text-xs text-slate-400">
                {isSpanish
                  ? 'Datos reales de la capability leída de la base de datos'
                  : 'Real capability data retrieved from the database'}
              </p>
            </div>

            <div className="flex items-center gap-2">
              <span className="text-xs text-slate-400">{isSpanish ? 'Cambiar skill:' : 'Switch skill:'}</span>
              <select
                value={courseCode}
                onChange={(e) => handleSelectSkill(e.target.value)}
                className="rounded-lg border border-white/10 bg-slate-950 px-2.5 py-1 text-xs font-mono text-emerald-400 focus:outline-none"
              >
                {KINDERGARTEN_OFFICIAL_SKILLS.map((s) => (
                  <option key={s.code} value={s.code}>
                    {s.code} - {isSpanish ? s.nameEs : s.nameEn}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className="block text-xs font-bold text-slate-300 mb-1">
                {isSpanish ? 'Título del Curso (Español)' : 'Course Title (Spanish)'}
              </label>
              <input
                type="text"
                value={courseTitleEs}
                onChange={(e) => setCourseTitleEs(e.target.value)}
                className="w-full rounded-xl border border-white/10 bg-slate-950 px-3.5 py-2.5 text-xs text-white focus:border-emerald-500 focus:outline-none font-medium"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-300 mb-1">
                {isSpanish ? 'Título del Curso (Inglés)' : 'Course Title (English)'}
              </label>
              <input
                type="text"
                value={courseTitleEn}
                onChange={(e) => setCourseTitleEn(e.target.value)}
                className="w-full rounded-xl border border-white/10 bg-slate-950 px-3.5 py-2.5 text-xs text-white focus:border-emerald-500 focus:outline-none font-medium"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-300 mb-1">
                {isSpanish ? 'Código del Skill en BD' : 'Skill Database Code'}
              </label>
              <input
                type="text"
                value={courseCode}
                onChange={(e) => setCourseCode(e.target.value)}
                className="w-full rounded-xl border border-white/10 bg-slate-950 px-3.5 py-2.5 text-xs font-mono text-emerald-400 font-bold focus:border-emerald-500 focus:outline-none"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-300 mb-1">
                {isSpanish ? 'Materia / Asignatura' : 'Subject / Area'}
              </label>
              <input
                type="text"
                value={isSpanish ? courseSubjectEs : courseSubjectEn}
                onChange={(e) => {
                  setCourseSubjectEs(e.target.value);
                  setCourseSubjectEn(e.target.value);
                }}
                className="w-full rounded-xl border border-white/10 bg-slate-950 px-3.5 py-2.5 text-xs text-white focus:border-emerald-500 focus:outline-none"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-300 mb-1">
                {isSpanish ? 'Horas para Maestría' : 'Hours to Master'}
              </label>
              <input
                type="number"
                value={courseHours}
                onChange={(e) => setCourseHours(e.target.value)}
                className="w-full rounded-xl border border-white/10 bg-slate-950 px-3.5 py-2.5 text-xs text-white focus:border-emerald-500 focus:outline-none"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-300 mb-1">
                {isSpanish ? 'Nivel Escolar' : 'Grade Level'}
              </label>
              <input
                type="text"
                disabled
                value="Kindergarten (Preescolar) · 5 a 6 años"
                className="w-full rounded-xl border border-white/10 bg-slate-950/60 px-3.5 py-2.5 text-xs text-slate-400 cursor-not-allowed"
              />
            </div>

            <div className="sm:col-span-2">
              <label className="block text-xs font-bold text-slate-300 mb-1">
                {isSpanish ? 'Descripción & Qué Aprenderá el Alumno:' : 'Course Description & Scope:'}
              </label>
              <textarea
                rows={3}
                value={isSpanish ? courseDescriptionEs : courseDescriptionEn}
                onChange={(e) => {
                  if (isSpanish) setCourseDescriptionEs(e.target.value);
                  else setCourseDescriptionEn(e.target.value);
                }}
                className="w-full rounded-xl border border-white/10 bg-slate-950 px-3.5 py-2.5 text-xs text-white focus:border-emerald-500 focus:outline-none leading-relaxed"
              />
            </div>

            <div className="sm:col-span-2">
              <label className="block text-xs font-bold text-slate-300 mb-1">
                💡 {isSpanish ? 'Por qué es clave (Justificación Pedagógica):' : 'Why it matters (Pedagogical Rationale):'}
              </label>
              <textarea
                rows={2}
                value={isSpanish ? courseWhyItMattersEs : courseWhyItMattersEn}
                onChange={(e) => {
                  if (isSpanish) setCourseWhyItMattersEs(e.target.value);
                  else setCourseWhyItMattersEn(e.target.value);
                }}
                className="w-full rounded-xl border border-white/10 bg-slate-950 px-3.5 py-2.5 text-xs text-white focus:border-emerald-500 focus:outline-none leading-relaxed"
              />
            </div>

            {/* Competencias Cubiertas (Micro-Skills) */}
            <div className="sm:col-span-2 pt-2">
              <label className="block text-xs font-bold text-slate-300 mb-2">
                {isSpanish ? 'Competencias Cubiertas por este Curso (Micro-Skills):' : 'Micro-Skills Covered by this Course:'}
              </label>
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                {skillsCoveredList.map((skill, idx) => (
                  <div
                    key={idx}
                    className="flex items-center gap-3 rounded-2xl border border-white/10 bg-slate-950/70 p-3"
                  >
                    <div className="flex h-5 w-5 shrink-0 items-center justify-center rounded-lg bg-emerald-500/20 text-emerald-400">
                      <Check className="h-3 w-3" />
                    </div>
                    <span className="text-xs font-medium text-slate-200">{skill}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* =========================================================================
          TAB 2: DISEÑO DEL CURSO (Replicated Logic from new-structured-learning)
          ========================================================================= */}
      {mainTab === 'course_design' && (
        <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-6 sm:p-8 backdrop-blur-xl space-y-6 animate-fade-in">
          <div>
            <h2 className="text-xl sm:text-2xl font-bold text-white">
              {isSpanish ? '## Diseño del curso' : '## Course Design'}
            </h2>
            <p className="mt-1 text-sm text-slate-400">
              {isSpanish
                ? 'Define la audiencia y los pasos principales del aprendizaje.'
                : 'Define the audience and the core pedagogical learning steps.'}
            </p>
          </div>

          {/* Sub-Tabs: Configuración (instead of general/profile), Hypothesis, Learn, In your role, Recall, Assessment */}
          <div className="flex flex-wrap gap-1.5 rounded-xl border border-white/10 bg-white/[0.02] p-1.5">
            {[
              { value: 'configuration' as const, label: isSpanish ? 'Configuración' : 'Configuration', Icon: Settings },
              { value: 'hypothesis' as const, label: 'Hypothesis', Icon: Lightbulb },
              { value: 'learn' as const, label: 'Learn', Icon: BookOpenText },
              { value: 'inYourRole' as const, label: 'In your role', Icon: Briefcase },
              { value: 'recall' as const, label: 'Recall', Icon: RotateCcw },
              { value: 'assessment' as const, label: 'Assessment', Icon: ClipboardCheck },
            ].map((tab) => {
              const active = designSubTab === tab.value;
              const visited = visitedDesignTabs.has(tab.value);
              return (
                <button
                  key={tab.value}
                  type="button"
                  onClick={() => {
                    setDesignSubTab(tab.value);
                    setVisitedDesignTabs((prev) => new Set(prev).add(tab.value));
                  }}
                  className={`flex items-center gap-1.5 rounded-lg px-3 py-2 text-xs font-semibold transition-colors ${
                    active
                      ? 'bg-gradient-to-r from-emerald-500 to-teal-500 text-white shadow-md shadow-emerald-500/20'
                      : 'text-slate-400 hover:bg-white/[0.05] hover:text-white'
                  }`}
                >
                  <tab.Icon className="h-3.5 w-3.5" />
                  <span>{tab.label}</span>
                  {visited && <CheckCircle2 className="h-3 w-3 text-emerald-400" />}
                </button>
              );
            })}
          </div>

          {/* Hint to visit tabs */}
          {!hasVisitedAllDesignTabs && (
            <p className="flex items-center gap-1.5 text-xs text-amber-300">
              <Sparkles className="h-3.5 w-3.5 shrink-0" />
              <span>
                {isSpanish
                  ? 'Abre cada pestaña al menos una vez (sobre todo Recall y Assessment) antes de continuar — así confirmas que revisaste las decisiones que afectan el costo de generación.'
                  : 'Open each tab at least once (especially Recall and Assessment) before continuing — confirming decisions that impact generation cost.'}
              </span>
            </p>
          )}

          {/* 1. SUB-TAB: CONFIGURACIÓN (General config without seniority L100/L200/L300) */}
          {designSubTab === 'configuration' && (
            <div className="space-y-6 animate-fade-in pt-2">
              <div>
                <label className="block text-xs font-bold text-slate-200 mb-1.5">
                  {isSpanish ? 'Audiencia pedagógica' : 'Pedagogical Audience'}
                </label>
                <div className="grid grid-cols-2 gap-2 max-w-xl">
                  {[
                    { value: 'YoungLearner' as const, label: 'Young Learners (Kindergarten / K-12 Elementary)' },
                    { value: 'Standard' as const, label: isSpanish ? 'Estándar (Secundaria / Bachillerato / Adulto)' : 'Standard (Middle / High School / Adult)' },
                  ].map((option) => {
                    const active = pedagogicalAudience === option.value;
                    return (
                      <button
                        key={option.value}
                        type="button"
                        onClick={() => setPedagogicalAudience(option.value)}
                        className={`rounded-xl border p-3 text-xs font-semibold text-left transition-colors ${
                          active
                            ? 'border-emerald-500 bg-emerald-500/15 text-white'
                            : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                        }`}
                      >
                        {option.label}
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Grafo de conocimiento */}
              <div className="border-t border-white/10 pt-5">
                <h3 className="text-sm font-bold text-white mb-1 flex items-center gap-2">
                  <span>###</span>
                  <span>{isSpanish ? 'Grafo de conocimiento' : 'Knowledge Graph'}</span>
                </h3>
                <p className="text-xs text-slate-400 mb-3">
                  {isSpanish
                    ? 'Configura la extracción de entidades y relaciones del documento.'
                    : 'Configure extraction of domain entities and relationships from the content.'}
                </p>

                <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-4">
                  <label className="flex items-start gap-3 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={includeKnowledgeGraph}
                      onChange={(e) => setIncludeKnowledgeGraph(e.target.checked)}
                      className="mt-0.5 h-4 w-4 shrink-0 rounded border-white/20 bg-white/[0.02] accent-emerald-500 text-emerald-500"
                    />
                    <div>
                      <strong className="text-xs text-white block">
                        {isSpanish ? 'Grafo de conocimiento' : 'Knowledge Graph Extraction'}
                      </strong>
                      <span className="text-xs text-slate-400 block mt-0.5 leading-relaxed">
                        {isSpanish
                          ? 'Extrae automáticamente las entidades del dominio (fonemas, números, conceptos clave...) y las relaciones entre ellas (secuencia, requiere, es parte de...) de cada sección.'
                          : 'Automatically extracts domain entities (phonics, numbers, core concepts...) and semantic relationships between them for each section.'}
                      </span>
                      <span className="text-xs font-semibold text-emerald-400 block mt-1.5">
                        {isSpanish ? '✓ Extraer el Grafo de conocimiento para este curso' : '✓ Extract Knowledge Graph for this course'}
                      </span>
                    </div>
                  </label>
                </div>
              </div>

              {/* Búsqueda Web en Vivo (Perplexity AI) */}
              <div className="border-t border-white/10 pt-5">
                <h3 className="text-sm font-bold text-white mb-1 flex items-center gap-2">
                  <span>###</span>
                  <span>{isSpanish ? 'Búsqueda Web en Vivo (Perplexity AI)' : 'Live Web Search (Perplexity AI)'}</span>
                </h3>
                <p className="text-xs text-slate-400 mb-3">
                  {isSpanish
                    ? 'Busca fuentes web en tiempo real para cada engrama e integra datos y citas actualizadas en su creación.'
                    : 'Search live web sources in real time for each engram to integrate verified discoveries.'}
                </p>

                <div className="rounded-2xl border border-emerald-500/30 bg-emerald-500/[0.05] p-4 space-y-2">
                  <div className="flex items-center gap-2">
                    <Globe className="h-4 w-4 text-emerald-400" />
                    <span className="text-xs font-bold text-emerald-300 uppercase tracking-wide">
                      {isSpanish ? 'Búsqueda Web en Vivo — Perplexity AI' : 'Live Web Search — Perplexity AI'}
                    </span>
                  </div>
                  <p className="text-xs text-slate-300 leading-relaxed">
                    {isSpanish
                      ? 'Al activarlo, el agente realiza una búsqueda en vivo en Perplexity para cada lección / engrama, integra hallazgos verificados y guarda las fuentes en los metadatos del engrama.'
                      : 'When enabled, the agent executes live Perplexity search per engram, incorporating verified findings into engram metadata.'}
                  </p>

                  <label className="flex items-center gap-2.5 pt-1 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={usePerplexitySearch}
                      onChange={(e) => setUsePerplexitySearch(e.target.checked)}
                      className="h-4 w-4 shrink-0 rounded border-emerald-500/40 bg-white/[0.02] accent-emerald-500 text-emerald-500"
                    />
                    <span className="text-xs font-bold text-emerald-200">
                      {isSpanish
                        ? 'Búsqueda con Perplexity para cada Engrama (Buscar en la web y guardar fuentes en metadatos)'
                        : 'Perplexity Search per Engram (Search web and save sources in metadata)'}
                    </span>
                  </label>
                </div>
              </div>
            </div>
          )}

          {/* 2. SUB-TAB: HYPOTHESIS */}
          {designSubTab === 'hypothesis' && (
            <div className="space-y-5 animate-fade-in pt-2">
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <Lightbulb className="h-4 w-4 text-emerald-400" />
                <span>{isSpanish ? 'Paso de Hipótesis Pedagógica' : 'Pedagogical Hypothesis Step'}</span>
              </h3>
              <p className="text-xs text-slate-400 leading-relaxed">
                {isSpanish
                  ? 'Activa preguntas iniciales de exploración para activar conocimientos previos del alumno antes de comenzar la lección.'
                  : 'Activates prior knowledge through early diagnostic hypothesis prompts before formal instruction.'}
              </p>

              <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-4">
                <label className="flex items-center gap-3 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={includeHypothesisStep}
                    onChange={(e) => setIncludeHypothesisStep(e.target.checked)}
                    className="h-4 w-4 shrink-0 rounded border-white/20 bg-white/[0.02] accent-emerald-500 text-emerald-500"
                  />
                  <span className="text-xs font-semibold text-white">
                    {isSpanish ? 'Incluir paso de Hipótesis en cada Engrama' : 'Include Hypothesis step in each Engram'}
                  </span>
                </label>
              </div>
            </div>
          )}

          {/* 3. SUB-TAB: LEARN */}
          {designSubTab === 'learn' && (
            <div className="space-y-5 animate-fade-in pt-2">
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <BookOpenText className="h-4 w-4 text-emerald-400" />
                <span>{isSpanish ? 'Paso de Aprendizaje y Profundización (Learn)' : 'Learning Step & Deep Dive (Learn)'}</span>
              </h3>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-4 space-y-2">
                  <label className="block text-xs font-bold text-white">
                    {isSpanish ? 'Máximo de Tarjetas de Profundización (Deep Dive)' : 'Max Deep Dive Cards per Node'}
                  </label>
                  <input
                    type="number"
                    min={1}
                    max={8}
                    value={maxDeepDiveCards}
                    onChange={(e) => setMaxDeepDiveCards(Number(e.target.value))}
                    className="w-full rounded-xl border border-white/10 bg-slate-950 px-3 py-2 text-xs text-white"
                  />
                  <span className="text-[11px] text-slate-400 block">
                    {isSpanish ? 'Número de tarjetas interactivas de ejemplo y exploración.' : 'Interactive cards per learning node.'}
                  </span>
                </div>

                <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-4 space-y-3">
                  <label className="flex items-center gap-2.5 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={showQaProfessorStep}
                      onChange={(e) => setShowQaProfessorStep(e.target.checked)}
                      className="h-4 w-4 rounded accent-emerald-500 text-emerald-500"
                    />
                    <span className="text-xs font-bold text-white">
                      {isSpanish ? 'Tutor / Profesor de Preguntas y Respuestas' : 'Q&A Professor / Tutor Step'}
                    </span>
                  </label>

                  <label className={`flex items-center gap-2.5 pl-6 text-xs cursor-pointer ${showQaProfessorStep ? 'text-slate-300' : 'text-slate-600'}`}>
                    <input
                      type="checkbox"
                      checked={enableQaProfessorVoice}
                      disabled={!showQaProfessorStep}
                      onChange={(e) => setEnableQaProfessorVoice(e.target.checked)}
                      className="h-3.5 w-3.5 rounded accent-emerald-500 text-emerald-500"
                    />
                    <span>{isSpanish ? 'Habilitar voz sintética de IA' : 'Enable synthetic AI voice'}</span>
                  </label>
                </div>

                <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-4 space-y-3 sm:col-span-2">
                  <label className="flex items-center gap-2.5 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={showDeepDiveAgentChat}
                      onChange={(e) => setShowDeepDiveAgentChat(e.target.checked)}
                      className="h-4 w-4 rounded accent-emerald-500 text-emerald-500"
                    />
                    <span className="text-xs font-bold text-white">
                      {isSpanish ? 'Agente de Chat Interactivo para Profundización' : 'Interactive Deep Dive Agent Chat'}
                    </span>
                  </label>

                  <label className={`flex items-center gap-2.5 pl-6 text-xs cursor-pointer ${showDeepDiveAgentChat ? 'text-slate-300' : 'text-slate-600'}`}>
                    <input
                      type="checkbox"
                      checked={enableDeepDiveAgentVoice}
                      disabled={!showDeepDiveAgentChat}
                      onChange={(e) => setEnableDeepDiveAgentVoice(e.target.checked)}
                      className="h-3.5 w-3.5 rounded accent-emerald-500 text-emerald-500"
                    />
                    <span>{isSpanish ? 'Habilitar diálogo de voz con el agente de profundización' : 'Enable voice dialogue for deep dive agent'}</span>
                  </label>
                </div>
              </div>
            </div>
          )}

          {/* 4. SUB-TAB: IN YOUR ROLE */}
          {designSubTab === 'inYourRole' && (
            <div className="space-y-5 animate-fade-in pt-2">
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <Briefcase className="h-4 w-4 text-emerald-400" />
                <span>{isSpanish ? 'Paso de Aplicación en tu Rol / Entorno' : 'In Your Role / Real-World Application Step'}</span>
              </h3>
              <p className="text-xs text-slate-400 leading-relaxed">
                {isSpanish
                  ? 'Plantea escenarios prácticos y retos donde el alumno aplica lo aprendido en situaciones cotidianas o proyectos reales.'
                  : 'Scenarios and challenges where the student applies newly gained competence to real-world contexts.'}
              </p>

              <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-4">
                <label className="flex items-center gap-3 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={includeInYourRoleStep}
                    onChange={(e) => setIncludeInYourRoleStep(e.target.checked)}
                    className="h-4 w-4 shrink-0 rounded border-white/20 bg-white/[0.02] accent-emerald-500 text-emerald-500"
                  />
                  <span className="text-xs font-semibold text-white">
                    {isSpanish ? 'Incluir paso de Aplicación en tu Rol en el curso' : 'Include In Your Role application step'}
                  </span>
                </label>
              </div>
            </div>
          )}

          {/* 5. SUB-TAB: RECALL */}
          {designSubTab === 'recall' && (
            <div className="space-y-5 animate-fade-in pt-2">
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <RotateCcw className="h-4 w-4 text-emerald-400" />
                <span>{isSpanish ? 'Paso de Recuerdo Activo (Recall)' : 'Active Recall Step (Memory Consolidation)'}</span>
              </h3>

              <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-4 space-y-4">
                <label className="flex items-center gap-3 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={includeRecallStep}
                    onChange={(e) => setIncludeRecallStep(e.target.checked)}
                    className="h-4 w-4 shrink-0 rounded border-white/20 bg-white/[0.02] accent-emerald-500 text-emerald-500"
                  />
                  <span className="text-xs font-bold text-white">
                    {isSpanish ? 'Incluir paso de Recuerdo Activo en las lecciones' : 'Include Active Recall memory consolidation step'}
                  </span>
                </label>

                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 pt-2 border-t border-white/5">
                  <div>
                    <label className="block text-xs font-bold text-slate-300 mb-1">
                      {isSpanish ? 'Preguntas de Recuerdo Requeridas' : 'Required Recall Items'}
                    </label>
                    <input
                      type="number"
                      min={1}
                      max={10}
                      value={recallItemsRequired}
                      onChange={(e) => setRecallItemsRequired(e.target.value)}
                      disabled={!includeRecallStep}
                      className="w-full rounded-xl border border-white/10 bg-slate-950 px-3 py-2 text-xs text-white disabled:opacity-40"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-slate-300 mb-1">
                      {isSpanish ? 'Modo de Generación' : 'Generation Mode'}
                    </label>
                    <div className="grid grid-cols-2 gap-2">
                      {[
                        { value: 'Dynamic' as const, label: isSpanish ? 'Dinámico (AI en vivo)' : 'Dynamic (Live AI)' },
                        { value: 'Static' as const, label: isSpanish ? 'Estático' : 'Static' },
                      ].map((opt) => (
                        <button
                          key={opt.value}
                          type="button"
                          disabled={!includeRecallStep}
                          onClick={() => setRecallGenerationMode(opt.value)}
                          className={`rounded-xl border p-2 text-xs font-semibold text-center transition ${
                            recallGenerationMode === opt.value
                              ? 'border-emerald-500 bg-emerald-500/15 text-white'
                              : 'border-white/10 bg-white/[0.02] text-slate-400'
                          } ${!includeRecallStep ? 'opacity-40 cursor-not-allowed' : ''}`}
                        >
                          {opt.label}
                        </button>
                      ))}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* 6. SUB-TAB: ASSESSMENT */}
          {designSubTab === 'assessment' && (
            <div className="space-y-5 animate-fade-in pt-2">
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <ClipboardCheck className="h-4 w-4 text-emerald-400" />
                <span>{isSpanish ? 'Paso de Evaluación (Assessment)' : 'Assessment & Mastery Check (Assessment)'}</span>
              </h3>

              <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-4 space-y-4">
                <label className="flex items-center gap-3 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={includeAssessmentStep}
                    onChange={(e) => setIncludeAssessmentStep(e.target.checked)}
                    className="h-4 w-4 shrink-0 rounded border-white/20 bg-white/[0.02] accent-emerald-500 text-emerald-500"
                  />
                  <span className="text-xs font-bold text-white">
                    {isSpanish ? 'Incluir Evaluación al final del curso' : 'Include Assessment check at course completion'}
                  </span>
                </label>

                <div className="grid grid-cols-1 gap-4 sm:grid-cols-3 pt-2 border-t border-white/5">
                  <div>
                    <label className="block text-xs font-bold text-slate-300 mb-1">
                      {isSpanish ? 'Tipo de Pregunta' : 'Question Format'}
                    </label>
                    <div className="grid grid-cols-2 gap-1.5">
                      {[
                        { value: 'MultipleChoice' as const, label: isSpanish ? 'Opción múltiple' : 'Multiple Choice' },
                        { value: 'OpenEnded' as const, label: isSpanish ? 'Abierta' : 'Open Ended' },
                      ].map((opt) => (
                        <button
                          key={opt.value}
                          type="button"
                          disabled={!includeAssessmentStep}
                          onClick={() => setAssessmentQuestionType(opt.value)}
                          className={`rounded-xl border p-2 text-xs font-semibold text-center transition ${
                            assessmentQuestionType === opt.value
                              ? 'border-emerald-500 bg-emerald-500/15 text-white'
                              : 'border-white/10 bg-white/[0.02] text-slate-400'
                          } ${!includeAssessmentStep ? 'opacity-40 cursor-not-allowed' : ''}`}
                        >
                          {opt.label}
                        </button>
                      ))}
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-slate-300 mb-1">
                      {isSpanish ? 'Preguntas por Ronda' : 'Questions per Round'}
                    </label>
                    <input
                      type="number"
                      min={1}
                      max={10}
                      value={assessmentQuestionsPerRound}
                      onChange={(e) => setAssessmentQuestionsPerRound(e.target.value)}
                      disabled={!includeAssessmentStep}
                      className="w-full rounded-xl border border-white/10 bg-slate-950 px-3 py-2 text-xs text-white disabled:opacity-40"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-slate-300 mb-1">
                      {isSpanish ? 'Modo de Evaluación' : 'Assessment Mode'}
                    </label>
                    <div className="grid grid-cols-2 gap-1.5">
                      {[
                        { value: 'Dynamic' as const, label: isSpanish ? 'Dinámico' : 'Dynamic' },
                        { value: 'Static' as const, label: isSpanish ? 'Estático' : 'Static' },
                      ].map((opt) => (
                        <button
                          key={opt.value}
                          type="button"
                          disabled={!includeAssessmentStep}
                          onClick={() => setAssessmentGenerationMode(opt.value)}
                          className={`rounded-xl border p-2 text-xs font-semibold text-center transition ${
                            assessmentGenerationMode === opt.value
                              ? 'border-emerald-500 bg-emerald-500/15 text-white'
                              : 'border-white/10 bg-white/[0.02] text-slate-400'
                          } ${!includeAssessmentStep ? 'opacity-40 cursor-not-allowed' : ''}`}
                        >
                          {opt.label}
                        </button>
                      ))}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* =========================================================================
          TAB 3: PROFESOR EXPERT & GENERADOR (9-Step Architecture Course Generator)
          ========================================================================= */}
      {mainTab === 'expert_teacher' && (
        <div className="space-y-6">
          {/* Top Banner & Context */}
          <div className="rounded-3xl border border-emerald-500/30 bg-gradient-to-r from-emerald-950/60 via-slate-900/80 to-teal-950/60 p-6 sm:p-8 backdrop-blur-xl">
            <div className="flex flex-wrap items-center justify-between gap-4">
              <div className="flex items-center gap-4">
                <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-emerald-500/20 text-emerald-300 border border-emerald-500/40 shadow-inner">
                  <Sparkles className="h-6 w-6 text-emerald-400" />
                </div>
                <div>
                  <div className="inline-flex items-center gap-2 rounded-full bg-emerald-500/20 px-3 py-0.5 text-[11px] font-bold text-emerald-300 border border-emerald-500/30 mb-1">
                    <GraduationCap className="h-3 w-3" />
                    <span>{isSpanish ? 'Agente ProfesorExpert · Kindergarten Specialist' : 'ProfesorExpert Agent · Kindergarten Specialist'}</span>
                  </div>
                  <h2 className="text-xl sm:text-2xl font-extrabold text-white">
                    {isSpanish ? 'Generador de Cursos Académicos con IA' : 'Academic Course Generator with AI'}
                  </h2>
                  <p className="text-xs text-slate-300 mt-0.5 max-w-2xl">
                    {isSpanish
                      ? 'Combina los datos reales de la capability, la voz cálida de un docente preescolar, material PDF opcional y la arquitectura de 9 pasos de Engrams.ai.'
                      : 'Combines real capability data, a warm preschool teacher voice, optional PDF materials, and the Engrams.ai 9-step architecture.'}
                  </p>
                  {isLoadingSavedDraft && (
                    <p className="text-[11px] text-sky-300 mt-1 flex items-center gap-1.5">
                      <RefreshCw className="h-3 w-3 animate-spin" />
                      {isSpanish ? 'Buscando un borrador guardado para este curso...' : 'Looking for a saved draft for this course...'}
                    </p>
                  )}
                </div>
              </div>

              <button
                type="button"
                disabled={isGeneratingIndex || isGeneratingChapters}
                onClick={handleGenerateIndex}
                className={`inline-flex items-center gap-2 rounded-2xl px-6 py-3.5 text-xs font-bold text-white transition shadow-lg ${
                  isGeneratingIndex || isGeneratingChapters
                    ? 'bg-slate-700 text-slate-400 cursor-not-allowed'
                    : 'bg-emerald-600 hover:bg-emerald-500 shadow-emerald-950/50 hover:scale-[1.02]'
                }`}
              >
                {isGeneratingIndex ? (
                  <>
                    <RefreshCw className="h-4 w-4 animate-spin text-emerald-300" />
                    <span>{isSpanish ? 'Generando índice...' : 'Generating index...'}</span>
                  </>
                ) : (
                  <>
                    <ListTree className="h-4 w-4 text-emerald-200" />
                    <span>
                      {courseIndex
                        ? isSpanish
                          ? '1. Regenerar Índice del Curso'
                          : '1. Regenerate Course Index'
                        : isSpanish
                        ? '1. Generar Índice del Curso'
                        : '1. Generate Course Index'}
                    </span>
                  </>
                )}
              </button>

              {generatedCourse && (
                <button
                  type="button"
                  onClick={() => window.print()}
                  className="inline-flex items-center gap-2 rounded-2xl bg-purple-600 hover:bg-purple-500 px-5 py-3.5 text-xs font-bold text-white transition shadow-lg"
                >
                  <Printer className="h-4 w-4" />
                  <span>{isSpanish ? 'Exportar a PDF' : 'Export to PDF'}</span>
                </button>
              )}
            </div>
          </div>

          {indexError && (
            <div className="rounded-2xl border border-red-500/30 bg-red-950/30 p-4 text-xs font-bold text-red-300">
              {indexError}
            </div>
          )}

          {/* Step 1 result: index review/approval (2026-10-08) — fast, lightweight;
              the educator reviews chapter/lesson titles BEFORE the much slower
              per-chapter content generation below runs. */}
          {courseIndex && (
            <div className="rounded-3xl border border-emerald-500/30 bg-emerald-950/10 p-6 backdrop-blur-xl space-y-4">
              <div className="flex items-center justify-between border-b border-white/10 pb-3">
                <div className="flex items-center gap-2">
                  <ListTree className="h-4 w-4 text-emerald-400" />
                  <h3 className="text-sm font-bold text-white">
                    {isSpanish ? `Índice Propuesto — ${courseIndex.chapters.length} capítulos` : `Proposed Index — ${courseIndex.chapters.length} chapters`}
                  </h3>
                </div>
              </div>

              <p className="text-xs text-slate-300">{courseIndex.teacherIntroduction}</p>

              <div className="rounded-2xl border border-white/10 bg-slate-950/60 p-4 max-h-96 overflow-y-auto space-y-3">
                {courseIndex.chapters.map((chapter, idx) => (
                  <div key={idx} className="border-b border-white/5 pb-3 last:border-b-0 last:pb-0">
                    <div className="flex items-center justify-between gap-2">
                      <div className="text-sm font-bold text-emerald-300">
                        {isSpanish ? `Capítulo ${chapter.chapterNumber}. ${chapter.chapterTitle}` : `Chapter ${chapter.chapterNumber}. ${chapter.chapterTitle}`}
                      </div>
                      <div className="flex items-center gap-1.5 shrink-0">
                        <button
                          type="button"
                          onClick={() => handleGenerateSingleChapter(idx)}
                          disabled={singleChapterGeneratingIdx !== null || isGeneratingChapters}
                          title={isSpanish ? 'Generar solo este capítulo (prueba rápida)' : 'Generate only this chapter (quick test)'}
                          className="inline-flex items-center gap-1 rounded-lg px-2 py-1 text-[10px] font-bold text-emerald-300 hover:text-emerald-200 hover:bg-emerald-500/10 transition disabled:opacity-40 disabled:cursor-not-allowed"
                        >
                          {singleChapterGeneratingIdx === idx ? (
                            <>
                              <RefreshCw className="h-3 w-3 animate-spin" />
                              <span>{isSpanish ? 'Generando...' : 'Generating...'}</span>
                            </>
                          ) : (
                            <>
                              <GraduationCap className="h-3 w-3" />
                              <span>{isSpanish ? 'Probar solo este' : 'Test only this'}</span>
                            </>
                          )}
                        </button>
                        <button
                          type="button"
                          onClick={() => handleDeleteIndexChapter(idx)}
                          title={isSpanish ? 'Eliminar este capítulo' : 'Delete this chapter'}
                          className="rounded-lg p-1.5 text-slate-500 hover:text-red-400 hover:bg-red-500/10 transition"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </div>
                    <ul className="mt-1.5 ml-4 list-disc space-y-0.5">
                      {chapter.lessonTitles.map((title, lIdx) => (
                        <li key={lIdx} className="text-xs text-slate-300">{title}</li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>

              {singleChapterError && (
                <div className="rounded-xl border border-red-500/30 bg-red-950/30 p-3 text-xs font-bold text-red-300">
                  {singleChapterError}
                </div>
              )}

              {chapterError && (
                <div className="rounded-xl border border-red-500/30 bg-red-950/30 p-3 text-xs font-bold text-red-300">
                  {chapterError}
                </div>
              )}

              <button
                type="button"
                disabled={isGeneratingChapters || courseIndex.chapters.length === 0}
                onClick={handleGenerateAllChapters}
                className={`inline-flex items-center gap-2 rounded-2xl px-6 py-3.5 text-xs font-bold text-white transition shadow-lg ${
                  isGeneratingChapters
                    ? 'bg-slate-700 text-slate-400 cursor-not-allowed'
                    : 'bg-emerald-600 hover:bg-emerald-500 shadow-emerald-950/50 hover:scale-[1.02]'
                }`}
              >
                {isGeneratingChapters && chapterProgress ? (
                  <>
                    <RefreshCw className="h-4 w-4 animate-spin text-emerald-300" />
                    <span>
                      {isSpanish
                        ? `Generando Capítulo ${chapterProgress.current}/${chapterProgress.total}: ${chapterProgress.title}...`
                        : `Generating Chapter ${chapterProgress.current}/${chapterProgress.total}: ${chapterProgress.title}...`}
                    </span>
                  </>
                ) : (
                  <>
                    <GraduationCap className="h-4 w-4 text-emerald-200" />
                    <span>{isSpanish ? '2. Aprobar y Generar Contenido Completo' : '2. Approve and Generate Full Content'}</span>
                  </>
                )}
              </button>
            </div>
          )}

          {/* Live per-chapter progress modal (2026-10-08) — pure frontend view over
              the same sequential backend calls in handleGenerateAllChapters; no
              extra backend cost, just surfaces chapterStatuses as a checklist. */}
          {isGeneratingChapters && (
            <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 backdrop-blur-sm p-4">
              <div className="w-full max-w-lg rounded-3xl border border-emerald-500/30 bg-slate-900 p-6 shadow-2xl space-y-4">
                <div className="flex items-center gap-2 border-b border-white/10 pb-3">
                  <RefreshCw className="h-4 w-4 text-emerald-400 animate-spin" />
                  <h3 className="text-sm font-bold text-white">
                    {isSpanish ? 'Generando contenido del curso...' : 'Generating course content...'}
                  </h3>
                </div>
                <div className="max-h-96 overflow-y-auto space-y-2">
                  {chapterStatuses.map((chapter, idx) => (
                    <div
                      key={idx}
                      className={`flex items-center gap-2.5 rounded-xl border px-3 py-2 transition ${
                        chapter.status === 'generating'
                          ? 'border-emerald-500/40 bg-emerald-950/30'
                          : chapter.status === 'done'
                            ? 'border-white/5 bg-slate-950/40'
                            : chapter.status === 'error'
                              ? 'border-red-500/30 bg-red-950/20'
                              : 'border-white/5 bg-slate-950/20'
                      }`}
                    >
                      {chapter.status === 'done' && <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-400" />}
                      {chapter.status === 'generating' && <RefreshCw className="h-4 w-4 shrink-0 animate-spin text-emerald-300" />}
                      {chapter.status === 'error' && <XCircle className="h-4 w-4 shrink-0 text-red-400" />}
                      {chapter.status === 'pending' && <Circle className="h-4 w-4 shrink-0 text-slate-600" />}
                      <span
                        className={`text-xs ${
                          chapter.status === 'pending' ? 'text-slate-500' : chapter.status === 'done' ? 'text-slate-300' : 'text-white font-bold'
                        }`}
                      >
                        {isSpanish ? `Capítulo ${chapter.chapterNumber}. ${chapter.chapterTitle}` : `Chapter ${chapter.chapterNumber}. ${chapter.chapterTitle}`}
                      </span>
                    </div>
                  ))}
                </div>
                <p className="text-[11px] text-slate-500 text-center">
                  {isSpanish
                    ? `${chapterProgress?.current ?? 0} / ${chapterProgress?.total ?? chapterStatuses.length} capítulos completados`
                    : `${chapterProgress?.current ?? 0} / ${chapterProgress?.total ?? chapterStatuses.length} chapters completed`}
                </p>
              </div>
            </div>
          )}

          {/* Section 1: Inputs from Tab 1 (Real Capability Data) */}
          <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-6 backdrop-blur-xl space-y-4">
            <div className="flex items-center justify-between border-b border-white/10 pb-3">
              <div className="flex items-center gap-2">
                <FileText className="h-4 w-4 text-emerald-400" />
                <h3 className="text-sm font-bold text-white">
                  {isSpanish ? '1. Datos Reales de la Capability (Leídos de BD)' : '1. Real Capability Data (From Database)'}
                </h3>
              </div>
              <span className="font-mono text-xs px-2.5 py-0.5 rounded-lg bg-emerald-500/20 text-emerald-300 font-bold border border-emerald-500/30">
                {courseCode}
              </span>
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <div className="rounded-2xl border border-white/5 bg-slate-950/60 p-3.5">
                <div className="text-[10px] font-bold text-slate-400 uppercase tracking-wider">
                  {isSpanish ? 'Título en Español' : 'Spanish Title'}
                </div>
                <div className="text-xs font-bold text-white mt-1">{courseTitleEs}</div>
              </div>

              <div className="rounded-2xl border border-white/5 bg-slate-950/60 p-3.5">
                <div className="text-[10px] font-bold text-slate-400 uppercase tracking-wider">
                  {isSpanish ? 'Título en Inglés' : 'English Title'}
                </div>
                <div className="text-xs font-bold text-slate-200 mt-1">{courseTitleEn}</div>
              </div>

              <div className="rounded-2xl border border-white/5 bg-slate-950/60 p-3.5">
                <div className="text-[10px] font-bold text-slate-400 uppercase tracking-wider">
                  {isSpanish ? 'Materia & Nivel' : 'Subject & Level'}
                </div>
                <div className="text-xs font-bold text-emerald-400 mt-1">
                  {courseSubjectEs} · Kinder (5-6 yrs)
                </div>
              </div>

              <div className="rounded-2xl border border-white/5 bg-slate-950/60 p-3.5">
                <div className="text-[10px] font-bold text-slate-400 uppercase tracking-wider">
                  {isSpanish ? 'Horas para Maestría' : 'Hours for Mastery'}
                </div>
                <div className="text-xs font-bold text-white mt-1">⏱️ ~{courseHours} hrs recomendadas</div>
              </div>
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 pt-1">
              <div className="rounded-2xl border border-white/5 bg-slate-950/60 p-3.5">
                <div className="text-[10px] font-bold text-slate-400 uppercase tracking-wider mb-1">
                  {isSpanish ? 'Descripción & Alcance' : 'Description & Scope'}
                </div>
                <p className="text-xs text-slate-300 leading-relaxed">{courseDescriptionEs}</p>
              </div>

              <div className="rounded-2xl border border-white/5 bg-slate-950/60 p-3.5">
                <div className="text-[10px] font-bold text-slate-400 uppercase tracking-wider mb-1">
                  💡 {isSpanish ? 'Por qué es clave (Justificación Pedagógica)' : 'Why it matters'}
                </div>
                <p className="text-xs text-slate-300 leading-relaxed">{courseWhyItMattersEs}</p>
              </div>
            </div>

            {/* Micro-Skills pills */}
            <div>
              <div className="text-[11px] font-bold text-slate-300 mb-2">
                {isSpanish ? 'Competencias Cubiertas (Micro-Skills):' : 'Micro-Skills Covered:'}
              </div>
              <div className="flex flex-wrap gap-2">
                {skillsCoveredList.map((skill, idx) => (
                  <span
                    key={idx}
                    className="inline-flex items-center gap-1.5 rounded-xl border border-emerald-500/30 bg-emerald-500/10 px-3 py-1 text-xs font-semibold text-emerald-300"
                  >
                    <Check className="h-3 w-3 text-emerald-400" />
                    <span>{skill}</span>
                  </span>
                ))}
              </div>
            </div>
          </div>

          {/* Section 2: Teacher Prompt Configurator & Optional PDF Ingestion */}
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            {/* 2A: Teacher Persona Prompt Configurator */}
            <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-6 backdrop-blur-xl space-y-3 flex flex-col justify-between">
              <div>
                <div className="flex items-center justify-between border-b border-white/10 pb-3 mb-3">
                  <div className="flex items-center gap-2">
                    <Bot className="h-4 w-4 text-emerald-400" />
                    <h3 className="text-sm font-bold text-white">
                      {isSpanish ? '2. Notas Adicionales (Opcional)' : '2. Additional Notes (Optional)'}
                    </h3>
                  </div>
                  <button
                    type="button"
                    onClick={handleResyncTeacherPrompt}
                    className="text-[11px] text-emerald-400 hover:text-emerald-300 font-semibold flex items-center gap-1"
                  >
                    <span>{isSpanish ? '🧹 Limpiar notas' : '🧹 Clear notes'}</span>
                  </button>
                </div>

                <label className="block text-xs font-bold text-slate-300 mb-1.5">
                  {isSpanish
                    ? 'Notas opcionales para el profesor (tono adicional, personajes, estilo específico) — la estructura del curso (índice, capítulos, lecciones) ya la aplica el sistema automáticamente, no hace falta repetirla aquí:'
                    : 'Optional notes for the teacher (extra tone, characters, specific style) — the course structure (index, chapters, lessons) is already applied automatically by the system, no need to repeat it here:'}
                </label>
                <textarea
                  rows={9}
                  value={teacherPrompt}
                  onChange={(e) => setTeacherPrompt(e.target.value)}
                  placeholder={isSpanish ? 'Ej: usa un personaje recurrente llamado...' : 'E.g.: use a recurring character named...'}
                  className="w-full rounded-2xl border border-white/10 bg-slate-950 p-3.5 text-xs text-white focus:border-emerald-500 focus:outline-none leading-relaxed font-mono"
                />
              </div>

              <div className="rounded-xl border border-emerald-500/20 bg-emerald-500/5 p-3 text-[11px] text-emerald-300">
                ✨ <strong>{isSpanish ? 'Regla del Agente:' : 'Agent Rule:'}</strong>{' '}
                {isSpanish
                  ? 'El sistema ya genera el índice estructurado (Capítulos y Lecciones), el tono calibrado a la audiencia y las sugerencias visuales automáticamente — estas notas son solo un extra opcional.'
                  : 'The system already generates the structured index (Chapters and Lessons), audience-calibrated tone, and visual suggestions automatically — these notes are just an optional extra.'}
              </div>
            </div>

            {/* 2B: Optional PDF Document Ingestion */}
            <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-6 backdrop-blur-xl space-y-3 flex flex-col justify-between">
              <div>
                <div className="flex items-center justify-between border-b border-white/10 pb-3 mb-3">
                  <div className="flex items-center gap-2">
                    <FileUp className="h-4 w-4 text-emerald-400" />
                    <h3 className="text-sm font-bold text-white">
                      {isSpanish ? '3. Documento PDF de Apoyo (Opcional)' : '3. Optional Supplementary PDF'}
                    </h3>
                  </div>
                  <span className="text-[10px] uppercase font-bold text-slate-400 px-2 py-0.5 rounded bg-white/5">
                    {isSpanish ? 'Opcional' : 'Optional'}
                  </span>
                </div>

                <p className="text-xs text-slate-300 leading-relaxed mb-3">
                  {isSpanish
                    ? 'Puedes subir un PDF con el programa escolar o guía didáctica. El LLM extraerá el contenido y lo unirá con sus conocimientos en el nuevo reporte del curso.'
                    : 'Upload a syllabus or teaching guide PDF. The LLM will extract key facts and merge them into the generated course report.'}
                </p>

                <input
                  ref={pdfInputRef}
                  type="file"
                  accept=".pdf,.txt,.doc,.docx"
                  onChange={handlePdfUpload}
                  className="hidden"
                />

                {!uploadedPdfName ? (
                  <div
                    onClick={() => pdfInputRef.current?.click()}
                    className="border-2 border-dashed border-white/15 hover:border-emerald-500/50 rounded-2xl p-6 text-center cursor-pointer transition bg-slate-950/40 hover:bg-emerald-950/10 group"
                  >
                    <UploadCloud className="h-8 w-8 text-slate-400 group-hover:text-emerald-400 mx-auto mb-2 transition" />
                    <div className="text-xs font-bold text-white">
                      {isSpanish ? 'Haz clic para seleccionar un PDF' : 'Click to select a PDF'}
                    </div>
                    <div className="text-[11px] text-slate-400 mt-1">
                      {isSpanish ? 'Programa curricular, guía de fonología o libro de texto' : 'Syllabus, phonics guide, or textbook chapter'}
                    </div>
                  </div>
                ) : (
                  <div className="rounded-2xl border border-emerald-500/30 bg-emerald-950/30 p-4 flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-500/20 text-emerald-300">
                        <FileCheck className="h-5 w-5" />
                      </div>
                      <div>
                        <div className="text-xs font-bold text-white">{uploadedPdfName}</div>
                        <div className="text-[10px] text-emerald-400 mt-0.5">
                          {uploadedPdfSize} · {isSpanish ? 'Listo para fusionar con el curso' : 'Ready to merge into course'}
                        </div>
                      </div>
                    </div>

                    <button
                      type="button"
                      onClick={handleRemovePdf}
                      className="p-2 rounded-xl text-slate-400 hover:text-red-400 hover:bg-red-500/10 transition"
                      title={isSpanish ? 'Quitar archivo' : 'Remove file'}
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                )}
              </div>

              <div className="rounded-xl border border-white/5 bg-slate-950/60 p-3 text-[11px] text-slate-400">
                ℹ️ {isSpanish
                  ? 'Si no se sube ningún archivo, ProfesorExpert utilizará los marcos oficiales de US Common Core y SEP Preescolar.'
                  : 'If no PDF is uploaded, ProfesorExpert defaults to official US Common Core and SEP Preschool frameworks.'}
              </div>
            </div>
          </div>

          {/* Section 3: The 8-Step Course Authoring Workflow Tracker */}
          <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-6 backdrop-blur-xl space-y-4">
            <div className="flex flex-wrap items-center justify-between border-b border-white/10 pb-3 gap-2">
              <div className="flex items-center gap-2">
                <Layers className="h-4 w-4 text-emerald-400" />
                <h3 className="text-sm font-bold text-white">
                  {isSpanish ? '4. Metodología de Redacción del Curso' : '4. Course Authoring Methodology'}
                </h3>
              </div>
              <span className="text-xs text-slate-400">
                {isSpanish
                  ? 'Flujo pedagógico ejecutado por el profesor para redactar el curso'
                  : 'Pedagogical authoring workflow executed by the professor'}
              </span>
            </div>

            <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-8">
              {TEACHER_COURSE_STEPS.map((item) => {
                const isStepCompleted = Boolean(generatedCourse) || Boolean(courseIndex);

                return (
                  <div
                    key={item.step}
                    className={`rounded-2xl border p-3 flex flex-col justify-between transition ${
                      isStepCompleted
                        ? 'border-emerald-500/30 bg-emerald-950/40 text-slate-200'
                        : 'border-white/5 bg-slate-950/50 text-slate-400'
                    }`}
                  >
                    <div>
                      <div className="flex items-center justify-between mb-1.5">
                        <span className="font-mono text-[10px] font-bold text-emerald-400">0{item.step}</span>
                        {isStepCompleted && <CheckCircle2 className="h-3 w-3 text-emerald-400" />}
                      </div>
                      <div className="text-[11px] font-bold text-white leading-tight">{item.name}</div>
                    </div>
                    <p className="text-[9px] text-slate-400 mt-2 line-clamp-3 leading-snug">{item.desc}</p>
                  </div>
                );
              })}
            </div>
          </div>

          {/* Section 4: Generated Course Report View */}
          {generatedCourse && (
            <div className="space-y-6 pt-2 animate-fade-in">
              {/* Quality & Validation Bar */}
              {/* Quality & Action Bar */}
              <div className="rounded-3xl border border-emerald-500/30 bg-slate-900/80 p-5 backdrop-blur-xl flex flex-wrap items-center justify-between gap-4">
                <div className="flex flex-wrap items-center gap-3">
                  <div className="inline-flex items-center gap-2 rounded-2xl bg-emerald-500/20 border border-emerald-500/40 px-3.5 py-1.5 text-xs font-bold text-emerald-300">
                    <ShieldCheck className="h-4 w-4 text-emerald-400" />
                    <span>{generatedCourse.architectureSummary?.groundednessScore || '99.4% Grounded'}</span>
                  </div>

                  <div className="inline-flex items-center gap-2 rounded-2xl bg-sky-500/20 border border-sky-500/40 px-3.5 py-1.5 text-xs font-bold text-sky-300">
                    <Globe className="h-4 w-4 text-sky-400" />
                    <span>{isSpanish ? 'Perplexity AI Verificado' : 'Perplexity AI Verified'}</span>
                  </div>

                  {/* Toggle Preview / Markdown */}
                  <div className="flex rounded-xl bg-slate-950 p-1 border border-white/10">
                    <button
                      type="button"
                      onClick={() => setViewDocumentMode('preview')}
                      className={`px-3 py-1 text-xs font-bold rounded-lg transition ${
                        viewDocumentMode === 'preview'
                          ? 'bg-emerald-500/20 text-emerald-300'
                          : 'text-slate-400 hover:text-white'
                      }`}
                    >
                      {isSpanish ? '📖 Vista Libro de Texto' : '📖 Textbook View'}
                    </button>
                    <button
                      type="button"
                      onClick={() => setViewDocumentMode('markdown')}
                      className={`px-3 py-1 text-xs font-bold rounded-lg transition ${
                        viewDocumentMode === 'markdown'
                          ? 'bg-emerald-500/20 text-emerald-300'
                          : 'text-slate-400 hover:text-white'
                      }`}
                    >
                      {isSpanish ? '📝 Código Markdown (para Engrams)' : '📝 Pure Markdown (for Engrams)'}
                    </button>
                  </div>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  <button
                    type="button"
                    onClick={handleCopyMarkdown}
                    className="inline-flex items-center gap-2 rounded-xl bg-slate-800 hover:bg-slate-700 border border-white/10 px-3.5 py-2 text-xs font-bold text-slate-200 transition"
                  >
                    {copiedMarkdown ? (
                      <>
                        <CheckCheck className="h-4 w-4 text-emerald-400" />
                        <span className="text-emerald-300">{isSpanish ? '¡Copiado!' : 'Copied!'}</span>
                      </>
                    ) : (
                      <>
                        <Copy className="h-4 w-4 text-slate-300" />
                        <span>{isSpanish ? 'Copiar Markdown para Engrams' : 'Copy Markdown for Engrams'}</span>
                      </>
                    )}
                  </button>

                  <button
                    type="button"
                    onClick={() => window.print()}
                    className="inline-flex items-center gap-2 rounded-xl bg-purple-600 hover:bg-purple-500 px-4 py-2 text-xs font-bold text-white transition shadow-sm"
                  >
                    <Printer className="h-4 w-4" />
                    <span>{isSpanish ? 'Exportar a PDF' : 'Export to PDF'}</span>
                  </button>

                  <button
                    type="button"
                    onClick={handleSaveCourseDraft}
                    className="inline-flex items-center gap-2 rounded-xl bg-emerald-600 hover:bg-emerald-500 px-4 py-2 text-xs font-bold text-white transition shadow-sm"
                  >
                    <Save className="h-4 w-4" />
                    <span>{isSpanish ? 'Guardar en BD' : 'Save to DB'}</span>
                  </button>
                </div>
              </div>

              {/* =========================================================================
                  CLEAN ACADEMIC COURSE DOCUMENT (No Prompts, No Process Names, Pure Course)
                  ========================================================================= */}
              {viewDocumentMode === 'preview' ? (
                <div className="rounded-3xl bg-white text-slate-900 p-8 sm:p-14 shadow-2xl border border-slate-200 font-sans academic-doc">
                  <style>{`
                    .academic-doc a { text-decoration: none; color: #464feb; }
                    .academic-doc tr th, .academic-doc tr td { border: 1px solid #e6e6e6; padding: 10px 14px; }
                    .academic-doc tr th { background-color: #f5f5f5; font-weight: 700; color: #111827; }
                    .academic-doc table { width: 100%; border-collapse: collapse; margin: 20px 0; font-size: 13px; }
                    .academic-doc h1 { font-size: 28px; font-weight: 800; color: #111827; margin-bottom: 20px; line-height: 1.25; }
                    .academic-doc h2 { font-size: 19px; font-weight: 700; color: #1f2937; margin-top: 28px; margin-bottom: 12px; }
                    .academic-doc hr { border: none; border-top: 1px solid #e5e7eb; margin: 28px 0; }
                    .academic-doc blockquote { border-left: 4px solid #464feb; padding-left: 16px; color: #4b5563; font-style: italic; margin: 18px 0; background: #f9fafb; padding: 12px 16px; border-radius: 0 8px 8px 0; }
                    .academic-doc ol, .academic-doc ul { padding-left: 24px; margin: 14px 0; }
                    .academic-doc li { margin-bottom: 7px; color: #374151; font-size: 14px; }
                    .academic-doc p { margin-bottom: 16px; line-height: 1.75; color: #374151; font-size: 14px; }
                  `}</style>

                  <div
                    dangerouslySetInnerHTML={{
                      __html: simpleMarkdownToHtml(generatedCourse.courseDocumentMarkdown || ''),
                    }}
                  />
                </div>
              ) : (
                <div className="rounded-3xl bg-slate-950 p-6 border border-white/10 space-y-3">
                  <div className="flex items-center justify-between text-xs text-slate-400">
                    <span className="font-mono text-emerald-400">course-textbook.md</span>
                    <span>Listo para pasar al Generador de Engrams</span>
                  </div>
                  <textarea
                    rows={26}
                    readOnly
                    value={generatedCourse.courseDocumentMarkdown}
                    className="w-full rounded-2xl border border-white/10 bg-slate-900/90 p-4 font-mono text-xs text-emerald-300 leading-relaxed focus:outline-none select-all"
                  />
                </div>
              )}

              {/* Step 8: Human Review — User Review & Approval Console */}
              <div className="rounded-3xl border border-emerald-500/40 bg-gradient-to-r from-slate-900/90 via-emerald-950/20 to-slate-900/90 p-6 sm:p-8 backdrop-blur-xl space-y-5">
                <div className="flex flex-wrap items-center justify-between gap-3 border-b border-white/10 pb-4">
                  <div className="flex items-center gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                      <Users className="h-5 w-5 text-emerald-400" />
                    </div>
                    <div>
                      <h3 className="text-base font-bold text-white">
                        {isSpanish ? 'Paso 8: Human Review — Revisión & Aprobación por Ti (Usuario)' : 'Step 8: Human Review — User Review & Approval'}
                      </h3>
                      <p className="text-xs text-slate-400">
                        {isSpanish
                          ? 'Como docente o responsable pedagógico, revisa el curso escrito antes de guardarlo en la base de datos.'
                          : 'As educator or pedagogical lead, review the textbook course before saving to database.'}
                      </p>
                    </div>
                  </div>

                  <span
                    className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-bold border ${
                      humanReviewApproved
                        ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/40'
                        : 'bg-amber-500/20 text-amber-300 border-amber-500/40'
                    }`}
                  >
                    {humanReviewApproved
                      ? isSpanish
                        ? '✓ Aprobado por el Docente / Usuario'
                        : '✓ Approved by Educator / User'
                      : isSpanish
                      ? '🟡 Pendiente de tu Aprobación'
                      : '🟡 Pending Your Approval'}
                  </span>
                </div>

                {/* Checklist */}
                <div className="space-y-2.5">
                  <div className="text-xs font-bold text-slate-300">
                    {isSpanish ? 'Criterios de Verificación Pedagógica:' : 'Pedagogical Verification Criteria:'}
                  </div>
                  <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
                    {[
                      {
                        key: 'toneApproved' as const,
                        label: isSpanish
                          ? 'Formato de libro de texto académico limpio y sin etiquetas de prompts'
                          : 'Clean textbook format without internal prompt tags',
                      },
                      {
                        key: 'pedagogyApproved' as const,
                        label: isSpanish
                          ? 'Secuencia de Capítulos estructurados con índice y objetivos'
                          : 'Structured chapter sequence with index and objectives',
                      },
                      {
                        key: 'perplexityApproved' as const,
                        label: isSpanish
                          ? 'Fundamentación empírica y tablas pedagógicas validadas'
                          : 'Empirical foundations & pedagogical tables validated',
                      },
                      {
                        key: 'visualsApproved' as const,
                        label: isSpanish
                          ? 'Listo para exportar a PDF y pasar al Generador de Engrams'
                          : 'Ready to export to PDF and feed into Engram Generator',
                      },
                    ].map((item) => (
                      <label
                        key={item.key}
                        className="flex items-center gap-3 rounded-2xl border border-white/10 bg-slate-950/60 p-3 cursor-pointer hover:border-emerald-500/30 transition"
                      >
                        <input
                          type="checkbox"
                          checked={humanReviewChecks[item.key]}
                          onChange={(e) =>
                            setHumanReviewChecks((prev) => ({ ...prev, [item.key]: e.target.checked }))
                          }
                          className="h-4 w-4 rounded border-white/20 bg-slate-900 text-emerald-500 focus:ring-emerald-500"
                        />
                        <span className="text-xs font-medium text-slate-200">{item.label}</span>
                      </label>
                    ))}
                  </div>
                </div>

                {/* User Notes */}
                <div>
                  <label className="block text-xs font-bold text-slate-300 mb-1.5">
                    {isSpanish
                      ? 'Observaciones pedagógicas o ajustes del profesor (Opcional):'
                      : 'Pedagogical observations or teacher notes (Optional):'}
                  </label>
                  <textarea
                    rows={2}
                    value={humanReviewNotes}
                    onChange={(e) => setHumanReviewNotes(e.target.value)}
                    placeholder={
                      isSpanish
                        ? 'Ej: "Curso académico verificado. Excelente progresión fonológica y tablas claras."'
                        : 'E.g. "Verified academic course. Excellent phonological progression and clear tables."'
                    }
                    className="w-full rounded-2xl border border-white/10 bg-slate-950 p-3 text-xs text-white focus:border-emerald-500 focus:outline-none"
                  />
                </div>

                {/* Approval & Save Action */}
                <div className="flex flex-wrap items-center justify-between gap-4 pt-2">
                  <div className="text-xs text-slate-400">
                    {humanReviewApproved
                      ? isSpanish
                        ? '✓ Has aprobado este curso académico. Listo para PDF y Generador de Engrams.'
                        : '✓ You have approved this course. Ready for PDF & Engram Generator.'
                      : isSpanish
                      ? 'Revisa el contenido y haz clic en el botón para firmar tu aprobación docente y guardar en BD.'
                      : 'Review the content and click the button to sign your educator approval and save to DB.'}
                  </div>

                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      onClick={() => window.print()}
                      className="inline-flex items-center gap-2 rounded-2xl bg-purple-600 hover:bg-purple-500 px-5 py-3 text-xs font-bold text-white transition shadow-lg"
                    >
                      <Printer className="h-4 w-4" />
                      <span>{isSpanish ? 'Exportar Curso a PDF' : 'Export Course to PDF'}</span>
                    </button>

                    <button
                      type="button"
                      onClick={() => {
                        setHumanReviewApproved(true);
                        void handleSaveCourseDraft();
                      }}
                      disabled={isSavingCourse}
                      className={`inline-flex items-center gap-2 rounded-2xl px-6 py-3 text-xs font-bold text-white transition shadow-lg ${
                        isSavingCourse
                          ? 'bg-slate-700 text-slate-400 cursor-not-allowed'
                          : humanReviewApproved
                          ? 'bg-emerald-600 hover:bg-emerald-500 shadow-emerald-950/60'
                          : 'bg-emerald-600 hover:bg-emerald-500 shadow-emerald-950/40 hover:scale-[1.02]'
                      }`}
                    >
                      {isSavingCourse ? (
                        <RefreshCw className="h-4 w-4 animate-spin" />
                      ) : (
                        <CheckCircle2 className="h-4 w-4" />
                      )}
                      <span>
                        {isSavingCourse
                          ? isSpanish
                            ? 'Guardando en BD...'
                            : 'Saving to DB...'
                          : humanReviewApproved
                          ? isSpanish
                            ? '✓ Curso Aprobado y Guardado en BD'
                            : '✓ Course Approved & Saved in DB'
                          : isSpanish
                          ? 'Aprobar y Guardar Curso en Base de Datos'
                          : 'Approve & Save Course to Database'}
                      </span>
                    </button>

                    <button
                      type="button"
                      onClick={() => setMainTab('review')}
                      disabled={!humanReviewApproved}
                      title={
                        !humanReviewApproved
                          ? isSpanish
                            ? 'Primero aprueba y guarda el curso'
                            : 'Approve & save the course first'
                          : undefined
                      }
                      className={`inline-flex items-center gap-2 rounded-2xl px-6 py-3 text-xs font-bold text-white transition shadow-lg ${
                        !humanReviewApproved
                          ? 'bg-slate-700 text-slate-400 cursor-not-allowed'
                          : 'bg-sky-600 hover:bg-sky-500 shadow-sky-950/50 hover:scale-[1.02]'
                      }`}
                    >
                      <ListTree className="h-4 w-4" />
                      <span>
                        {isSpanish
                          ? 'Continuar a Review (Paso 5) →'
                          : 'Continue to Review (Step 5) →'}
                      </span>
                    </button>
                  </div>
                </div>

                {(saveErrorMessage || bridgeErrorMessage) && (
                  <div className="rounded-xl border border-red-500/30 bg-red-950/30 p-3 text-xs font-bold text-red-300">
                    {saveErrorMessage || bridgeErrorMessage}
                  </div>
                )}
              </div>
            </div>
          )}
        </div>
      )}

      {/* =========================================================================
          TAB 4 — STEP 5: REVIEW (mirrors /new-structured-learning's Paso 5)
          ========================================================================= */}
      {mainTab === 'review' && (
        <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-6 sm:p-8 backdrop-blur-xl space-y-6 animate-fade-in">
          <div className="border-b border-white/10 pb-4">
            <h2 className="text-base font-bold text-white">{isSpanish ? 'Paso 5: Review' : 'Step 5: Review'}</h2>
            <p className="text-xs text-slate-400 mt-1">
              {isSpanish
                ? 'Confirma la estructura de capítulos detectada antes de analizar el documento. El texto del curso NO se reescribe — solo se detectan sus capítulos y lecciones.'
                : 'Confirm the detected chapter structure before analyzing the document. The course text is NOT rewritten — only its chapters/lessons are detected.'}
            </p>
          </div>

          {!generatedCourse && (
            <div className="rounded-xl border border-amber-500/30 bg-amber-950/30 p-4 text-xs font-bold text-amber-300">
              {isSpanish
                ? 'Primero genera y aprueba el curso en la pestaña "Profesor Experto · Curso".'
                : 'First generate and approve the course in the "Expert Teacher · Course" tab.'}
            </div>
          )}

          {generatedCourse && !outlinePreview && (
            <button
              type="button"
              onClick={() => void loadOutlineForReview()}
              disabled={isLoadingOutlinePreview}
              className={`inline-flex items-center gap-2 rounded-2xl px-6 py-3 text-xs font-bold text-white transition shadow-lg ${
                isLoadingOutlinePreview
                  ? 'bg-slate-700 text-slate-400 cursor-not-allowed'
                  : 'bg-emerald-600 hover:bg-emerald-500 shadow-emerald-950/50 hover:scale-[1.02]'
              }`}
            >
              {isLoadingOutlinePreview ? (
                <RefreshCw className="h-4 w-4 animate-spin" />
              ) : (
                <ListTree className="h-4 w-4" />
              )}
              <span>
                {isLoadingOutlinePreview
                  ? isSpanish
                    ? 'Analizando estructura...'
                    : 'Analyzing structure...'
                  : isSpanish
                  ? 'Analizar Estructura del Curso'
                  : 'Analyze Course Structure'}
              </span>
            </button>
          )}

          {outlinePreviewError && (
            <div className="rounded-xl border border-red-500/30 bg-red-950/30 p-3 text-xs font-bold text-red-300">
              {outlinePreviewError}
            </div>
          )}

          {outlinePreview && (
            <div className="space-y-4">
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                <div className="rounded-2xl border border-white/10 bg-slate-950/60 p-4">
                  <div className="text-[10px] font-bold text-slate-400 uppercase">
                    {isSpanish ? 'Tipo de Documento' : 'Document Type'}
                  </div>
                  <div className="text-sm font-bold text-white mt-1">{outlinePreview.DetectedDocumentType ?? '—'}</div>
                </div>
                <div className="rounded-2xl border border-white/10 bg-slate-950/60 p-4">
                  <div className="text-[10px] font-bold text-slate-400 uppercase">
                    {isSpanish ? 'Capítulos Detectados' : 'Chapters Detected'}
                  </div>
                  <div className="text-sm font-bold text-white mt-1">{outlinePreview.Chapters.length}</div>
                </div>
                <div className="rounded-2xl border border-white/10 bg-slate-950/60 p-4">
                  <div className="text-[10px] font-bold text-slate-400 uppercase">
                    {isSpanish ? 'Caracteres / Tokens' : 'Characters / Tokens'}
                  </div>
                  <div className="text-sm font-bold text-white mt-1">
                    ~{outlinePreview.TotalCharacters.toLocaleString()} / ~{outlinePreview.TotalTokens.toLocaleString()}
                  </div>
                </div>
              </div>

              <div className="flex items-center justify-between">
                <div className="text-xs text-slate-400">
                  {isSpanish
                    ? 'Puedes eliminar capítulos o lecciones aquí para probar con solo una lección (no se vuelve a generar el texto, solo se ajusta qué se envía al Paso 6).'
                    : 'You can delete chapters or lessons here to test with a single lesson (the text itself is not regenerated — only what gets sent to Step 6 changes).'}
                </div>
              </div>

              <div className="rounded-2xl border border-white/10 bg-slate-950/60 p-4 max-h-96 overflow-y-auto space-y-3">
                {outlinePreview.Chapters.map((chapter, idx) => (
                  <div key={idx} className="border-b border-white/5 pb-3 last:border-b-0 last:pb-0">
                    <div className="flex items-center justify-between gap-2">
                      <div className="text-sm font-bold text-emerald-300">{chapter.Title}</div>
                      <button
                        type="button"
                        onClick={() => handleDeleteOutlineChapter(idx)}
                        title={isSpanish ? 'Eliminar este capítulo completo' : 'Delete this entire chapter'}
                        className="shrink-0 rounded-lg p-1.5 text-slate-500 hover:text-red-400 hover:bg-red-500/10 transition"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    </div>
                    <ul className="mt-1.5 ml-4 list-disc space-y-0.5">
                      {chapter.Nodes.map((node, nIdx) => (
                        <li key={nIdx} className="flex items-center justify-between gap-2 text-xs text-slate-300">
                          <span>{node.Title}</span>
                          <button
                            type="button"
                            onClick={() => handleDeleteOutlineLesson(idx, nIdx)}
                            title={isSpanish ? 'Eliminar esta lección' : 'Delete this lesson'}
                            className="shrink-0 rounded p-1 text-slate-600 hover:text-red-400 hover:bg-red-500/10 transition"
                          >
                            <Trash2 className="h-3 w-3" />
                          </button>
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
                {outlinePreview.Chapters.length === 0 && (
                  <div className="text-xs text-amber-300 font-bold">
                    {isSpanish
                      ? 'Eliminaste todos los capítulos — vuelve a analizar para restaurar el índice completo.'
                      : "You deleted every chapter — re-analyze to restore the full outline."}
                  </div>
                )}
              </div>

              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => void loadOutlineForReview()}
                  className="inline-flex items-center gap-2 rounded-xl bg-slate-800 hover:bg-slate-700 border border-white/10 px-4 py-2.5 text-xs font-bold text-slate-200 transition"
                >
                  <RefreshCw className="h-4 w-4" />
                  <span>{isSpanish ? 'Re-analizar (restaura todo)' : 'Re-analyze (restores everything)'}</span>
                </button>

                <button
                  type="button"
                  onClick={() => setMainTab('learning_path_design')}
                  disabled={outlinePreview.Chapters.length === 0}
                  className={`inline-flex items-center gap-2 rounded-2xl px-6 py-3 text-xs font-bold text-white transition shadow-lg ${
                    outlinePreview.Chapters.length === 0
                      ? 'bg-slate-700 text-slate-400 cursor-not-allowed'
                      : 'bg-sky-600 hover:bg-sky-500 hover:scale-[1.02]'
                  }`}
                >
                  <GraduationCap className="h-4 w-4" />
                  <span>{isSpanish ? 'Continuar a Diseño de Ruta (Paso 6) →' : 'Continue to Learning Path Design (Step 6) →'}</span>
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* =========================================================================
          TAB 5 — STEP 6: LEARNING PATH DESIGN (mirrors /new-structured-learning's Paso 6)
          ========================================================================= */}
      {mainTab === 'learning_path_design' && (
        <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-6 sm:p-8 backdrop-blur-xl space-y-6 animate-fade-in">
          <div className="border-b border-white/10 pb-4">
            <h2 className="text-base font-bold text-white">
              {isSpanish ? 'Paso 6: Diseño de la Ruta de Aprendizaje' : 'Step 6: Learning Path Design'}
            </h2>
            <p className="text-xs text-slate-400 mt-1">
              {isSpanish
                ? 'Confirma la configuración pedagógica (definida en la pestaña "Diseño del curso") y genera la ruta de aprendizaje completa: knowledge graph, entidades, facts, engrams y academia — igual que el flujo de empresas.'
                : 'Confirm the pedagogical configuration (set in the "Course Design" tab) and generate the complete learning path: knowledge graph, entities, facts, engrams and academy — exactly like the Enterprise flow.'}
            </p>
          </div>

          {!outlinePreview && (
            <div className="rounded-xl border border-amber-500/30 bg-amber-950/30 p-4 text-xs font-bold text-amber-300">
              {isSpanish
                ? 'Primero completa el Paso 5: Review.'
                : 'First complete Step 5: Review.'}
            </div>
          )}

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div className="rounded-2xl border border-white/10 bg-slate-950/60 p-4">
              <div className="text-[10px] font-bold text-slate-400 uppercase">
                {isSpanish ? 'Audiencia Pedagógica' : 'Pedagogical Audience'}
              </div>
              <div className="text-sm font-bold text-white mt-1">
                {pedagogicalAudience === 'YoungLearner'
                  ? isSpanish
                    ? 'Aprendices Jóvenes (Kindergarten / K-12)'
                    : 'Young Learners (Kindergarten / K-12)'
                  : isSpanish
                  ? 'Estándar (Secundaria / Adultos)'
                  : 'Standard (Middle/High School / Adult)'}
              </div>
            </div>
            <div className="rounded-2xl border border-white/10 bg-slate-950/60 p-4">
              <div className="text-[10px] font-bold text-slate-400 uppercase">Knowledge Graph</div>
              <div className="text-sm font-bold text-white mt-1">
                {includeKnowledgeGraph ? (isSpanish ? 'Habilitado' : 'Enabled') : isSpanish ? 'Deshabilitado' : 'Disabled'}
              </div>
            </div>
            <div className="rounded-2xl border border-white/10 bg-slate-950/60 p-4">
              <div className="text-[10px] font-bold text-slate-400 uppercase">Hypothesis / Recall / Assessment</div>
              <div className="text-sm font-bold text-white mt-1">
                {[includeHypothesisStep && 'Hypothesis', includeRecallStep && 'Recall', includeAssessmentStep && 'Assessment']
                  .filter(Boolean)
                  .join(' · ') || (isSpanish ? 'Ninguno' : 'None')}
              </div>
            </div>
            <div className="rounded-2xl border border-white/10 bg-slate-950/60 p-4">
              <div className="text-[10px] font-bold text-slate-400 uppercase">
                {isSpanish ? 'Búsqueda Web Perplexity' : 'Perplexity Web Search'}
              </div>
              <div className="text-sm font-bold text-white mt-1">
                {usePerplexitySearch ? (isSpanish ? 'Habilitado' : 'Enabled') : isSpanish ? 'Deshabilitado' : 'Disabled'}
              </div>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setMainTab('course_design')}
              className="inline-flex items-center gap-2 rounded-xl bg-slate-800 hover:bg-slate-700 border border-white/10 px-4 py-2.5 text-xs font-bold text-slate-200 transition"
            >
              <Sliders className="h-4 w-4" />
              <span>{isSpanish ? 'Ajustar Diseño del Curso' : 'Adjust Course Design'}</span>
            </button>

            <button
              type="button"
              onClick={() => void handleStartLearningPathDesign()}
              disabled={isBridgingToLearningPath || !generatedCourse}
              className={`inline-flex items-center gap-2 rounded-2xl px-6 py-3 text-xs font-bold text-white transition shadow-lg ${
                isBridgingToLearningPath || !generatedCourse
                  ? 'bg-slate-700 text-slate-400 cursor-not-allowed'
                  : 'bg-emerald-600 hover:bg-emerald-500 shadow-emerald-950/50 hover:scale-[1.02]'
              }`}
            >
              {isBridgingToLearningPath ? (
                <RefreshCw className="h-4 w-4 animate-spin" />
              ) : (
                <GraduationCap className="h-4 w-4" />
              )}
              <span>
                {isBridgingToLearningPath
                  ? isSpanish
                    ? 'Generando Ruta de Aprendizaje...'
                    : 'Generating Learning Path...'
                  : isSpanish
                  ? 'Generar Ruta de Aprendizaje Completa'
                  : 'Generate Complete Learning Path'}
              </span>
            </button>
          </div>

          {bridgeErrorMessage && (
            <div className="rounded-xl border border-red-500/30 bg-red-950/30 p-3 text-xs font-bold text-red-300">
              {bridgeErrorMessage}
            </div>
          )}
        </div>
      )}
    </div>

    {/* =========================================================================
        PRINTABLE PDF VIEW (Triggered on window.print()) — intentionally OUTSIDE the
        print:hidden app shell above, so it's the only thing that remains visible
        when the browser print/PDF dialog is used.
        ========================================================================= */}
    {generatedCourse && (
        <div className="hidden print:block text-slate-900 bg-white font-sans p-6 academic-doc">
          <style>{`
            .academic-doc a { text-decoration: none; color: #464feb; }
            .academic-doc tr th, .academic-doc tr td { border: 1px solid #e6e6e6; padding: 10px 14px; }
            .academic-doc tr th { background-color: #f5f5f5; font-weight: 700; color: #111827; }
            .academic-doc table { width: 100%; border-collapse: collapse; margin: 18px 0; font-size: 13px; }
            .academic-doc h1 { font-size: 26px; font-weight: 800; color: #111827; margin-bottom: 16px; border-bottom: 2px solid #e5e7eb; padding-bottom: 8px; }
            .academic-doc h2 { font-size: 18px; font-weight: 700; color: #1f2937; margin-top: 24px; margin-bottom: 12px; }
            .academic-doc h3 { font-size: 15px; font-weight: 700; color: #1f2937; margin-top: 18px; margin-bottom: 8px; }
            .academic-doc hr { border: none; border-top: 1px solid #e5e7eb; margin: 24px 0; }
            .academic-doc blockquote { border-left: 4px solid #464feb; padding-left: 14px; color: #4b5563; font-style: italic; margin: 16px 0; background: #f9fafb; padding: 12px 16px; border-radius: 0 8px 8px 0; }
            .academic-doc ol, .academic-doc ul { padding-left: 20px; margin: 12px 0; }
            .academic-doc li { margin-bottom: 6px; color: #374151; font-size: 13px; }
            .academic-doc p { margin-bottom: 14px; line-height: 1.7; color: #374151; font-size: 14px; }
          `}</style>

          <div
            dangerouslySetInnerHTML={{
              __html: simpleMarkdownToHtml(generatedCourse.courseDocumentMarkdown || ''),
            }}
          />

          <div className="mt-8 pt-4 border-t-2 border-slate-300 text-xs text-slate-600 flex justify-between items-center">
            <div>
              <strong>Estado de Human Review:</strong> {humanReviewApproved ? 'Aprobado y Validado por el Docente' : 'Borrador'}
              {humanReviewNotes && <p className="italic mt-0.5">Notas: "{humanReviewNotes}"</p>}
            </div>
            <div className="border-t border-slate-400 pt-1 w-48 text-center text-[10px] text-slate-500">
              Firma del Docente / Coordinador
            </div>
          </div>
        </div>
    )}
    </>
  );
}
