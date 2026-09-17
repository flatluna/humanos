import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ProcessLearningWizardStepper } from '@/components/wizard/ProcessLearningWizardStepper';
import { ProcessUploadStep } from '@/components/wizard/steps/ProcessUploadStep';
import { ProcessStructureReviewStep } from '@/components/wizard/steps/ProcessStructureReviewStep';
import { WorkRequirementsReviewStep } from '@/components/wizard/steps/WorkRequirementsReviewStep';
import { LearningContentMatchStep } from '@/components/wizard/steps/LearningContentMatchStep';
import { ContentGapReviewStep } from '@/components/wizard/steps/ContentGapReviewStep';
import { LearningAssignmentReviewStep } from '@/components/wizard/steps/LearningAssignmentReviewStep';
import { EmployeeAssignmentStep } from '@/components/wizard/steps/EmployeeAssignmentStep';
import { ProcessLearningSummaryModal } from '@/components/wizard/ProcessLearningSummaryModal';
import type {
  ProcessDocumentStructureDto,
  ProcessStepDto,
  WorkStepRequirementsAnalysisResultDto,
  RelevantEngramsResponseDto,
  LearningContentCoverageResponseDto,
  LearningContentGapDraftResponseDto,
  StepLearningAssignmentResponseDto,
  EmployeeStepLearningAssignmentResponseDto,
} from '@/api/processDocumentsApi';
import type { TenantPersonDto } from '@/api/orgStructureApi';

const mockStep: ProcessStepDto = {
  step_id: 'step-001',
  step_number: '1.1',
  step_name: 'Inspeccionar Equipo de Seguridad',
  objective: 'Verificar arnés y línea de vida antes de iniciar trabajos en altura.',
  required_activities: ['Revisar costuras del arnés', 'Validar fecha de vigencia'],
  required_skills: ['Inspección visual de EPP', 'Trabajo en alturas'],
  deliverables: ['Checklist de inspección firmado'],
  completion_condition: 'Arnés aprobado sin defectos visibles',
  substeps: [
    {
      substep_id: 'substep-1',
      substep_number: '1.1.1',
      name: 'Verificación de herrajes',
      description: 'Inspeccionar anillos D',
    },
  ],
  checklists: ['Verificar mosquetón de doble seguro'],
  source_pages: [1, 2],
};

const mockStructure: ProcessDocumentStructureDto = {
  document_id: 'doc-123',
  process_name: 'Procedimiento de Mantenimiento Seguro',
  document_title: 'SOP-MANT-002: Procedimiento de Mantenimiento',
  source_file: {
    file_name: 'sop-mantenimiento.pdf',
    content_type: 'application/pdf',
    storage_path: 'processes/sop-mantenimiento.pdf',
  },
  chapters: [
    {
      chapter_id: 'ch-01',
      chapter_number: '1',
      title: 'Capítulo 1: Preparación y Seguridad',
      description: 'Protocolos de inicio seguro',
      modules: [],
      steps: [mockStep],
    },
  ],
  tables: [],
  appendices: [],
  warnings: [],
  processing_status: 'completed',
};

const mockRequirements: WorkStepRequirementsAnalysisResultDto = {
  documentId: 'doc-123',
  stepId: 'step-001',
  stepNumber: '1.1',
  workStep: 'Inspeccionar Equipo de Seguridad',
  employeeMustBeAbleTo: [
    'Identificar desgastes en costuras y correas',
    'Operar mosquetones de seguridad de doble acción',
  ],
  requiredSkillAreas: {
    softSkills: [
      { name: 'Atención al Detalle', reason: 'Detectar micro-fisuras en herrajes' },
    ],
    businessSkills: [
      { name: 'Cumplimiento Normativo', reason: 'Adherirse a estándares OSHA / NOM' },
    ],
    technicalSkills: [
      { name: 'Inspección de EPP', reason: 'Criterios de descarte de equipo' },
    ],
  },
  requiredTools: [
    { name: 'Arnés de cuerpo completo', useInStep: 'Protección contra caídas' },
    { name: 'Checklist digital móvil', useInStep: 'Registro del estado del equipo' },
  ],
  completionEvidence: [
    'Checklist digital completado con fotografías',
    'Etiqueta verde de inspección vigente colocada',
  ],
  assumptions: ['El trabajador cuenta con capacitación básica de inducción'],
  warnings: ['Nunca usar equipo con sospecha de impacto previo'],
  sourcePages: [1, 2],
};

const mockRelevantEngrams: RelevantEngramsResponseDto = {
  documentId: 'doc-123',
  stepId: 'step-001',
  stepNumber: '1.1',
  workStep: 'Inspeccionar Equipo de Seguridad',
  selectedClasses: [
    {
      learningPathId: 'lp-01',
      learningPathName: 'Seguridad en Operaciones',
      classId: 'class-01',
      className: 'Seguridad Industrial Avanzada',
      classDescription: 'Fundamentos de prevención de caídas y EPP.',
      selectedEngrams: [
        {
          engramId: 'engram-01',
          engramName: 'Revisión técnica de arneses',
          engramDescription: 'Técnica de inspección punto a punto para líneas de vida y arnés.',
          supportedRequirements: ['Identificar desgastes en costuras y correas'],
          relevanceReason: 'Directamente cubre el requerimiento de inspección de EPP.',
          relevanceScore: 95,
          criticality: 'critical',
        },
      ],
    },
  ],
  requirementCoverage: [
    {
      requirement: 'Identificar desgastes en costuras y correas',
      requirementType: 'ability',
      coverageStatus: 'covered',
      supportingEngramIds: ['engram-01'],
      notes: 'Totalmente cubierto por la clase técnica',
    },
  ],
  uncoveredRequirements: ['Checklist digital móvil'],
  assumptions: [],
  warnings: [],
  sourcePages: [1, 2],
};

const mockCoverage: LearningContentCoverageResponseDto = {
  documentId: 'doc-123',
  stepId: 'step-001',
  stepNumber: '1.1',
  workStep: 'Inspeccionar Equipo de Seguridad',
  overallCoverageStatus: 'partially_covered',
  coverageSummary: 'Cubre el 75% de los requerimientos operativos.',
  coveredRequirements: [
    {
      requirement: 'Identificar desgastes en costuras y correas',
      criticality: 'critical',
      supportingEngramIds: ['engram-01'],
    },
  ],
  contentGaps: [
    {
      requirement: 'Checklist digital móvil',
      requirementType: 'tool',
      criticality: 'critical',
      currentCoverage: 'not_covered',
      gapReason: 'No existe contenido que instruya en el uso de la app de checklist.',
      recommendedGapType: 'new_engram_in_existing_class',
      existingLearningPathId: 'lp-01',
      existingClassId: 'class-01',
    },
  ],
  assumptions: [],
  warnings: [],
  sourcePages: [1, 2],
};

const mockGapDraft: LearningContentGapDraftResponseDto = {
  documentId: 'doc-123',
  stepId: 'step-001',
  stepNumber: '1.1',
  workStep: 'Inspeccionar Equipo de Seguridad',
  overallCoverageStatus: 'partially_covered',
  draftCreated: true,
  engramDrafts: [
    {
      draftReference: 'DRAFT-ENG-001',
      proposedEngramTitle: 'Uso del Checklist Móvil de Seguridad',
      proposedEngramDescription: 'Guía práctica para capturar evidencias fotográficas y firmas.',
      capabilitySupported: 'Registro digital de inspección',
      workStepRequirementSupported: 'Checklist digital móvil',
      reasonNeeded: 'Cierra la brecha en herramientas digitales.',
      parentLearningPathId: 'lp-01',
      parentLearningPathName: 'Seguridad en Operaciones',
      parentClassId: 'class-01',
      parentClassName: 'Seguridad Industrial Avanzada',
      status: 'proposed',
    },
  ],
  classDrafts: [],
  catalogItemsChecked: [],
  assumptions: [],
  warnings: [],
  sourcePages: [1, 2],
};

const mockAssignment: StepLearningAssignmentResponseDto = {
  documentId: 'doc-123',
  stepId: 'step-001',
  stepNumber: '1.1',
  workStep: 'Inspeccionar Equipo de Seguridad',
  assignmentStatus: 'ready_with_content_gaps',
  assignmentMarkdown: '# Asignación de Aprendizaje: Inspección de Seguridad\n\nContenido estándar del paso.',
  learningPathIds: ['lp-01'],
  classIds: ['class-01'],
  engramIds: ['engram-01'],
  coverageStatus: 'partially_covered',
  contentGapDraftReferences: ['DRAFT-ENG-001'],
  blockingContentGap: false,
  assumptions: [],
  warnings: [],
  sourcePages: [1, 2],
};

const mockPeople: TenantPersonDto[] = [
  {
    personId: 'emp-001',
    displayName: 'Carlos Hernández',
    email: 'carlos.hernandez@empresa.com',
    jobRoleId: 'role-1',
    jobRoleTitle: 'Técnico de Mantenimiento Jr.',
    departmentId: 'dept-1',
    departmentName: 'Mantenimiento',
    managerId: null,
    managerDisplayName: null,
    orgRole: 'Employee',
    isActive: true,
  },
];

const mockEmployeeAssignment: EmployeeStepLearningAssignmentResponseDto = {
  assignmentId: 'asg-001',
  employeeId: 'emp-001',
  documentId: 'doc-123',
  stepId: 'step-001',
  stepNumber: '1.1',
  workStep: 'Inspeccionar Equipo de Seguridad',
  employeeAssignmentStatus: 'assigned',
  employeeMustBeAbleTo: ['Identificar desgastes en costuras y correas'],
  reportedExperienceSummary: ['Experiencia previa en talleres mecánicos'],
  assignedClasses: [
    {
      learningPathId: 'lp-01',
      learningPathName: 'Seguridad en Operaciones',
      classId: 'class-01',
      className: 'Seguridad Industrial Avanzada',
      classAssignmentStatus: 'required',
      assignedEngrams: [
        {
          engramId: 'engram-01',
          engramName: 'Revisión técnica de arneses',
          assignmentStatus: 'required',
          assignmentReason: 'Esencial para su rol operativo',
          employeeEvidenceLevel: 'unknown',
          employeeEvidenceSummary: 'Sin registros de capacitación previa en el sistema',
        },
      ],
    },
  ],
  contentGaps: [],
  expectedWorkEvidence: ['Checklist firmado'],
  assignmentMarkdown: '# Asignación Personalizada para Carlos Hernández\n\nEnfoque especial en herrajes.',
  assumptions: [],
  warnings: [],
  createdAt: '2026-03-30T12:00:00Z',
};

describe('ProcessLearningWizard Components Suite', () => {
  it('ProcessLearningWizardStepper renders all 7 steps correctly', () => {
    const onSelectStep = vi.fn();
    render(
      <ProcessLearningWizardStepper
        currentStep={2}
        maxStepReached={2}
        stepStates={{
          1: 'completed',
          2: 'in_progress',
          3: 'not_started',
          4: 'not_started',
          5: 'not_started',
          6: 'not_started',
          7: 'not_started',
        }}
        onSelectStep={onSelectStep}
      />
    );

    // Verify step labels are rendered
    expect(screen.getByText('Cargar Proceso')).toBeDefined();
    expect(screen.getByText('Revisar Pasos')).toBeDefined();
    expect(screen.getByText('Requisitos de Trabajo')).toBeDefined();
    expect(screen.getByText('Emparejar Aprendizaje')).toBeDefined();
    expect(screen.getByText('Cobertura y Brechas')).toBeDefined();
    expect(screen.getByText('Asignación de Aprendizaje')).toBeDefined();
    expect(screen.getByText('Asignar a Colaborador')).toBeDefined();

    // Clicking completed step 1 calls onSelectStep(1)
    const step1Btn = screen.getByText('Cargar Proceso').closest('button');
    expect(step1Btn).toBeDefined();
    fireEvent.click(step1Btn!);
    expect(onSelectStep).toHaveBeenCalledWith(1);
  });

  it('ProcessUploadStep renders file dropzone and structure info when uploaded', () => {
    const onStructureLoaded = vi.fn();
    const onContinue = vi.fn();

    const { rerender } = render(
      <ProcessUploadStep
        initialStructure={null}
        initialUploadResult={null}
        onStructureLoaded={onStructureLoaded}
        onNext={onContinue}
      />
    );

    expect(screen.getByText('Subir Documento de Proceso (PDF)')).toBeDefined();

    // Rerender with structure loaded
    rerender(
      <ProcessUploadStep
        initialStructure={mockStructure}
        initialUploadResult={{
          document_id: 'doc-123',
          process_name: 'Procedimiento de Mantenimiento Seguro',
          status: 'completed',
          message: 'ok',
          chapters_identified: 1,
          steps_identified: 1,
          warnings: [],
        } as any}
        onStructureLoaded={onStructureLoaded}
        onNext={onContinue}
      />
    );

    expect(screen.getByText('Procedimiento de Mantenimiento Seguro')).toBeDefined();

    // Click continue
    const continueBtn = screen.getByText(/Revisar Pasos del Proceso/i);
    fireEvent.click(continueBtn);
    expect(onContinue).toHaveBeenCalledTimes(1);
  });

  it('ProcessStructureReviewStep enables editing and selecting steps', () => {
    const onSelectStep = vi.fn();
    const onUpdateStructure = vi.fn();
    const onToggleExclude = vi.fn();
    const onContinue = vi.fn();

    render(
      <ProcessStructureReviewStep
        structure={mockStructure}
        selectedStepId="step-001"
        excludedStepIds={new Set()}
        onSelectStep={onSelectStep}
        onUpdateStructure={onUpdateStructure}
        onToggleExcludeStep={onToggleExclude}
        onApproveAndContinue={onContinue}
      />
    );

    expect(screen.getAllByText('Inspeccionar Equipo de Seguridad').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Revisar costuras del arnés')).toBeDefined();

    // Click edit step
    const editBtn = screen.getByText('Editar');
    fireEvent.click(editBtn);

    // Form inputs should be visible
    expect(screen.getByText('Guardar')).toBeDefined();

    // Click continue button
    const nextBtn = screen.getByText(/Aprobar Estructura y Analizar Requisitos/i);
    fireEvent.click(nextBtn);
    expect(onContinue).toHaveBeenCalledTimes(1);
  });

  it('WorkRequirementsReviewStep renders cognitive, operational, and behavioral requirements', () => {
    const onAnalyzeStep = vi.fn();
    const onUpdate = vi.fn();
    const onContinue = vi.fn();

    render(
      <WorkRequirementsReviewStep
        step={mockStep}
        requirements={mockRequirements}
        isLoading={false}
        onAnalyzeStep={onAnalyzeStep}
        onUpdateRequirements={onUpdate}
        onApproveAndContinue={onContinue}
      />
    );

    expect(screen.getByText('Identificar desgastes en costuras y correas')).toBeDefined();
    expect(screen.getByText('Atención al Detalle')).toBeDefined();
    expect(screen.getByText('Cumplimiento Normativo')).toBeDefined();
    expect(screen.getByText('Arnés de cuerpo completo')).toBeDefined();

    // Click continue
    const approveBtn = screen.getByText(/Aprobar Requisitos y Emparejar Aprendizaje/i);
    fireEvent.click(approveBtn);
    expect(onContinue).toHaveBeenCalledTimes(1);
  });

  it('LearningContentMatchStep allows toggling engrams and displays warning for critical items', () => {
    const onSearchAndMatch = vi.fn();
    const onContinue = vi.fn();

    render(
      <LearningContentMatchStep
        step={mockStep}
        relevantEngramsResult={mockRelevantEngrams}
        isLoading={false}
        onSearchAndMatch={onSearchAndMatch}
        onApproveAndContinue={onContinue}
      />
    );

    expect(screen.getByText('Seguridad Industrial Avanzada')).toBeDefined();
    expect(screen.getByText('Revisión técnica de arneses')).toBeDefined();

    // Click remove engram icon button
    const removeBtn = screen.getByTitle('Quitar');
    fireEvent.click(removeBtn);

    // Critical removal warning should appear
    expect(screen.getByText(/Advertencia: "Revisión técnica de arneses" es un Engrama crítico/i)).toBeDefined();

    // Restore button appears and restores engram
    const restoreBtn = screen.getByText(/Restaurar "Revisión técnica de arneses"/i);
    fireEvent.click(restoreBtn);

    // Click continue
    const continueBtn = screen.getByText(/Aceptar Aprendizaje y Evaluar Cobertura/i);
    fireEvent.click(continueBtn);
    expect(onContinue).toHaveBeenCalledTimes(1);
  });

  it('ContentGapReviewStep renders coverage status and gap proposal', () => {
    const onEvaluate = vi.fn();
    const onContinue = vi.fn();

    render(
      <ContentGapReviewStep
        step={mockStep}
        coverageResult={mockCoverage}
        gapDraftResult={mockGapDraft}
        isLoading={false}
        onEvaluateCoverage={onEvaluate}
        onApproveAndContinue={onContinue}
      />
    );

    expect(screen.getByText('Checklist digital móvil')).toBeDefined();
    expect(screen.getByText('Uso del Checklist Móvil de Seguridad')).toBeDefined();

    // Continue to assignment
    const nextBtn = screen.getByText(/Continuar a Generación de Asignación/i);
    fireEvent.click(nextBtn);
    expect(onContinue).toHaveBeenCalledTimes(1);
  });

  it('LearningAssignmentReviewStep displays assignment markdown and triggers copy', () => {
    const onGenerate = vi.fn();
    const onContinue = vi.fn();

    render(
      <LearningAssignmentReviewStep
        step={mockStep}
        assignmentResult={mockAssignment}
        isLoading={false}
        onGenerateAssignment={onGenerate}
        onApproveAndContinue={onContinue}
      />
    );

    expect(screen.getByText(/Asignación de Aprendizaje: Inspección de Seguridad/i)).toBeDefined();

    // Click continue to employee assignment
    const approveBtn = screen.getByText(/Aprobar y Personalizar para un Colaborador/i);
    fireEvent.click(approveBtn);
    expect(onContinue).toHaveBeenCalledTimes(1);
  });

  it('EmployeeAssignmentStep renders employee list and displays personalized assignment', () => {
    const onPersonalize = vi.fn();
    const onFinish = vi.fn();
    const onAssignAnother = vi.fn();

    const { rerender } = render(
      <EmployeeAssignmentStep
        step={mockStep}
        tenantPeople={mockPeople}
        assignmentResult={null}
        isLoading={false}
        onPersonalizeAndAssign={onPersonalize}
        onFinish={onFinish}
        onAssignAnother={onAssignAnother}
      />
    );

    // Initial state: picker renders Carlos Hernández
    expect(screen.getByText('Carlos Hernández')).toBeDefined();

    // Click on employee to select
    fireEvent.click(screen.getByText('Carlos Hernández'));

    // Rerender with assignment result
    rerender(
      <EmployeeAssignmentStep
        step={mockStep}
        tenantPeople={mockPeople}
        assignmentResult={mockEmployeeAssignment}
        isLoading={false}
        onPersonalizeAndAssign={onPersonalize}
        onFinish={onFinish}
        onAssignAnother={onAssignAnother}
      />
    );

    expect(screen.getByText(/Asignación Personalizada para Carlos Hernández/i)).toBeDefined();
    expect(screen.getByText(/Finalizar Wizard/i)).toBeDefined();

    // Click finish
    const finishBtn = screen.getByText(/Finalizar Wizard/i);
    fireEvent.click(finishBtn);
    expect(onFinish).toHaveBeenCalledTimes(1);
  });

  it('ProcessLearningSummaryModal renders all summary metrics and step progress table', () => {
    const onClose = vi.fn();
    const onSelectStep = vi.fn();

    const mockStats = [
      {
        stepId: 'step-001',
        stepNumber: '1.1',
        stepName: 'Inspeccionar Equipo de Seguridad',
        chapterTitle: 'Capítulo 1: Preparación y Seguridad',
        isExcluded: false,
        hasRequirements: true,
        classesCount: 1,
        engramsCount: 1,
        coverageStatus: 'partially_covered' as const,
        assignmentStatus: 'ready_with_content_gaps' as const,
        isAssigned: true,
        assignedEmployeeName: 'Carlos Hernández',
        draftsCount: 1,
      },
    ];

    render(
      <ProcessLearningSummaryModal
        isOpen={true}
        onClose={onClose}
        structure={mockStructure}
        stepSummaries={mockStats}
        onOpenStep={onSelectStep}
      />
    );

    expect(screen.getByText('Resumen Global de Cobertura del Proceso')).toBeDefined();
    expect(screen.getByText(/Procedimiento de Mantenimiento Seguro/i)).toBeDefined();

    // Click jump to step
    const jumpBtn = screen.getByText('Abrir Paso');
    fireEvent.click(jumpBtn);
    expect(onSelectStep).toHaveBeenCalledWith('step-001');
  });
});
