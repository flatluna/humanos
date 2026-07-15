import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import StudioHeader from './StudioHeader';
import StudioStepIndicator from './StudioStepIndicator';
import ObjectiveField from './ObjectiveField';
import IntensitySelector from './IntensitySelector';
import MaterialUploader from './MaterialUploader';
import GenerateBlueprintButton from './GenerateBlueprintButton';
import { Intensity, StudioMaterial, StudioObjectiveForm } from '../../types';
import { getCapabilityDomains, BackendCapabilityDomain } from '../../lib/api/domainsApi';
import { startCapabilityCreation, BackendRawMaterialItem } from '../../lib/api/studioApi';
import { updateStudioRun } from '../../lib/studioRunStore';

const initialForm: StudioObjectiveForm = {
  objective: '',
  intensity: 'serious',
  materials: [],
};

/** Text-like extensions we can read as plain text on the client (no PDF/DOCX extraction here yet). */
const TEXT_EXTENSIONS = ['.txt', '.md'];

const ObjectiveStep: React.FC = () => {
  const navigate = useNavigate();
  const [form, setForm] = useState<StudioObjectiveForm>(initialForm);
  const [objectiveError, setObjectiveError] = useState<string>('');
  const [isProcessing, setIsProcessing] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const [domains, setDomains] = useState<BackendCapabilityDomain[]>([]);
  const [selectedDomainId, setSelectedDomainId] = useState<string>('');
  const [domainsError, setDomainsError] = useState<string | null>(null);

  // Raw File objects, kept alongside the StudioMaterial metadata list so
  // their real text content can be read at submit time (StudioMaterial
  // itself only carries fileName/fileType/fileSize — no content).
  const rawFilesRef = useRef<Map<string, File>>(new Map());

  const minObjectiveLength = 20;

  useEffect(() => {
    getCapabilityDomains()
      .then((result) => {
        setDomains(result);
        if (result.length > 0) {
          setSelectedDomainId(result[0].CapabilityDomainId);
        }
      })
      .catch((err) =>
        setDomainsError(err instanceof Error ? err.message : 'No se pudieron cargar los dominios.')
      );
  }, []);

  // Simulate file upload with delay
  const simulateFileUpload = (material: StudioMaterial): Promise<StudioMaterial> => {
    return new Promise((resolve) => {
      const uploadDelay = Math.random() * 2000 + 1000; // 1-3 seconds
      setTimeout(() => {
        resolve({
          ...material,
          status: 'uploaded',
        });
      }, uploadDelay);
    });
  };

  const handleAddMaterial = useCallback(
    async (file: File) => {
      const newMaterial: StudioMaterial = {
        id: Date.now().toString(),
        fileName: file.name,
        fileType: '.' + file.name.split('.').pop()?.toLowerCase(),
        fileSize: file.size,
        status: file.size > 25 * 1024 * 1024 ? 'error' : 'uploading',
        errorMessage: file.size > 25 * 1024 * 1024 ? 'Archivo muy grande' : undefined,
      };

      rawFilesRef.current.set(newMaterial.id, file);

      setForm((prev) => ({
        ...prev,
        materials: [...prev.materials, newMaterial],
      }));

      if (newMaterial.status === 'uploading') {
        const uploaded = await simulateFileUpload(newMaterial);
        setForm((prev) => ({
          ...prev,
          materials: prev.materials.map((m) =>
            m.id === uploaded.id ? uploaded : m
          ),
        }));
      }
    },
    []
  );

  const handleRemoveMaterial = useCallback((id: string) => {
    rawFilesRef.current.delete(id);
    setForm((prev) => ({
      ...prev,
      materials: prev.materials.filter((m) => m.id !== id),
    }));
  }, []);

  const handleObjectiveChange = (value: string) => {
    setForm((prev) => ({
      ...prev,
      objective: value,
    }));
    if (objectiveError && value.length >= minObjectiveLength) {
      setObjectiveError('');
    }
  };

  const handleIntensityChange = (value: Intensity) => {
    setForm((prev) => ({
      ...prev,
      intensity: value,
    }));
  };

  // Check if form is valid
  const isObjectiveValid = form.objective.length >= minObjectiveLength;
  const hasUploadingFile = form.materials.some((m) => m.status === 'uploading');
  const hasErrorFile = form.materials.some((m) => m.status === 'error');
  const isFormValid = isObjectiveValid && !hasUploadingFile && !hasErrorFile && !!selectedDomainId;

  const handleGenerateBlueprint = async () => {
    // Validate objective
    if (!isObjectiveValid) {
      setObjectiveError(
        `Escribe el objetivo de la capability antes de continuar. Mínimo ${minObjectiveLength} caracteres.`
      );
      return;
    }

    if (!selectedDomainId) {
      setSubmitError('Selecciona un dominio antes de continuar.');
      return;
    }

    setIsProcessing(true);
    setSubmitError(null);

    try {
      // The objective itself always counts as one raw material (the
      // backend requires at least one). Uploaded text-like files (.txt/.md)
      // contribute their real content too; PDFs/DOCX aren't extracted on
      // the client yet, so only their file name is passed as a label-only
      // note (still useful context, just not full text).
      const rawMaterials: BackendRawMaterialItem[] = [
        { type: 'UserNote', label: 'Objetivo', content: form.objective },
      ];

      for (const material of form.materials) {
        if (material.status !== 'uploaded') continue;

        const file = rawFilesRef.current.get(material.id);
        if (file && TEXT_EXTENSIONS.includes(material.fileType)) {
          const content = await file.text();
          rawMaterials.push({ type: 'UserNote', label: material.fileName, content });
        } else {
          rawMaterials.push({
            type: 'UserNote',
            label: material.fileName,
            content: `(Archivo adjunto "${material.fileName}" — extracción de contenido no disponible todavía para este formato.)`,
          });
        }
      }

      const status = await startCapabilityCreation({
        capabilityDomainId: selectedDomainId,
        capabilityGoal: form.objective,
        rawMaterials,
      });

      updateStudioRun({ runId: status.RunId, capabilityDomainId: selectedDomainId });
      navigate('/studio/blueprint');
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : 'No se pudo iniciar la generación del blueprint.');
    } finally {
      setIsProcessing(false);
    }
  };

  return (
    <div className="max-w-2xl mx-auto">
      <StudioHeader />
      <StudioStepIndicator activeStep={1} />

      <div className="bg-white rounded-lg shadow p-8">
        <div className="mb-8">
          <label className="block text-lg font-semibold text-gray-900 mb-2">Dominio</label>
          {domainsError && <p className="text-red-600 text-sm mb-2">{domainsError}</p>}
          <select
            value={selectedDomainId}
            onChange={(e) => setSelectedDomainId(e.target.value)}
            className="w-full px-3 py-2 border border-gray-300 rounded-lg text-gray-900 bg-white"
            disabled={domains.length === 0}
          >
            {domains.length === 0 && <option value="">Cargando dominios...</option>}
            {domains.map((domain) => (
              <option key={domain.CapabilityDomainId} value={domain.CapabilityDomainId}>
                {domain.Name}
              </option>
            ))}
          </select>
        </div>

        <ObjectiveField
          value={form.objective}
          onChange={handleObjectiveChange}
          error={objectiveError}
        />

        <IntensitySelector
          value={form.intensity}
          onChange={handleIntensityChange}
        />

        <MaterialUploader
          materials={form.materials}
          onAddMaterial={handleAddMaterial}
          onRemoveMaterial={handleRemoveMaterial}
        />

        {submitError && (
          <div className="mb-6 p-4 bg-red-50 border-2 border-red-300 rounded-lg">
            <p className="text-red-900 font-medium">No se pudo generar el blueprint</p>
            <p className="text-red-800 text-sm mt-1">{submitError}</p>
          </div>
        )}

        <GenerateBlueprintButton
          isValid={isFormValid}
          isLoading={isProcessing}
          onClick={handleGenerateBlueprint}
          hasUploadingFile={hasUploadingFile}
          hasErrorFile={hasErrorFile}
        />
      </div>
    </div>
  );
};

export default ObjectiveStep;

