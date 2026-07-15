import React, { useRef, useState } from 'react';
import { Upload, X, FileText } from 'lucide-react';
import { StudioMaterial } from '../../types';

interface MaterialUploaderProps {
  materials: StudioMaterial[];
  onAddMaterial: (file: File) => void;
  onRemoveMaterial: (id: string) => void;
}

const ACCEPTED_FORMATS = ['.pdf', '.txt', '.md', '.docx'];
const MAX_FILE_SIZE = 25 * 1024 * 1024; // 25MB
const MAX_FILES = 10;

const formatFileSize = (bytes: number): string => {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return Math.round((bytes / Math.pow(k, i)) * 10) / 10 + ' ' + sizes[i];
};

const getFileExtension = (fileName: string): string => {
  return '.' + fileName.split('.').pop()?.toLowerCase();
};

const MaterialUploader: React.FC<MaterialUploaderProps> = ({
  materials,
  onAddMaterial,
  onRemoveMaterial,
}) => {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [dragActive, setDragActive] = useState(false);

  const handleDrag = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    if (e.type === 'dragenter' || e.type === 'dragover') {
      setDragActive(true);
    } else if (e.type === 'dragleave') {
      setDragActive(false);
    }
  };

  const validateAndAddFile = (file: File) => {
    const fileExtension = getFileExtension(file.name);

    // Check format
    if (!ACCEPTED_FORMATS.includes(fileExtension)) {
      onAddMaterial(new File([new Blob()], file.name, { type: 'text/plain' }));
      return;
    }

    // Check size
    if (file.size > MAX_FILE_SIZE) {
      onAddMaterial(new File([new Blob()], file.name, { type: 'text/plain' }));
      return;
    }

    // Check max files
    if (materials.length >= MAX_FILES) {
      return;
    }

    onAddMaterial(file);
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);

    const { files } = e.dataTransfer;
    if (files && files[0]) {
      for (let i = 0; i < Math.min(files.length, MAX_FILES - materials.length); i++) {
        validateAndAddFile(files[i]);
      }
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { files } = e.target;
    if (files && files[0]) {
      for (let i = 0; i < Math.min(files.length, MAX_FILES - materials.length); i++) {
        validateAndAddFile(files[i]);
      }
    }
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const canAddMore = materials.length < MAX_FILES;

  return (
    <div className="mb-8">
      <label className="block text-lg font-semibold text-gray-900 mb-4">
        Material <span className="text-sm font-normal text-gray-500">(opcional)</span>
      </label>

      <div
        onDragEnter={handleDrag}
        onDragLeave={handleDrag}
        onDragOver={handleDrag}
        onDrop={handleDrop}
        className={`border-2 border-dashed rounded-lg p-8 text-center transition-colors cursor-pointer ${
          dragActive
            ? 'border-purple-600 bg-purple-50'
            : 'border-gray-300 bg-gray-50 hover:bg-gray-100'
        } ${!canAddMore ? 'opacity-50 cursor-not-allowed' : ''}`}
        onClick={() => canAddMore && fileInputRef.current?.click()}
      >
        <Upload className="mx-auto mb-3 text-gray-400" size={32} />
        <p className="font-medium text-gray-900 mb-1">
          Arrastra archivos aquí o haz clic para seleccionar
        </p>
        <p className="text-sm text-gray-600">PDF, transcripciones y notas</p>
        {!canAddMore && (
          <p className="text-sm text-red-600 mt-2 font-medium">
            Límite de {MAX_FILES} archivos alcanzado
          </p>
        )}
      </div>

      <input
        ref={fileInputRef}
        type="file"
        multiple
        accept={ACCEPTED_FORMATS.join(',')}
        onChange={handleChange}
        className="hidden"
        disabled={!canAddMore}
      />

      {materials.length > 0 && (
        <div className="mt-6">
          <h3 className="text-sm font-semibold text-gray-900 mb-3">
            Material agregado ({materials.length}/{MAX_FILES})
          </h3>
          <div className="space-y-2">
            {materials.map((material) => (
              <div
                key={material.id}
                className={`flex items-center justify-between p-3 rounded-lg border transition-colors ${
                  material.status === 'error'
                    ? 'bg-red-50 border-red-200'
                    : material.status === 'uploading'
                    ? 'bg-blue-50 border-blue-200'
                    : material.status === 'uploaded'
                    ? 'bg-green-50 border-green-200'
                    : 'bg-gray-50 border-gray-200'
                }`}
              >
                <div className="flex items-center gap-3 flex-1 min-w-0">
                  <FileText
                    size={18}
                    className={
                      material.status === 'error'
                        ? 'text-red-600'
                        : material.status === 'uploaded'
                        ? 'text-green-600'
                        : 'text-gray-600'
                    }
                  />
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-gray-900 truncate">
                      {material.fileName}
                    </p>
                    <div className="flex items-center gap-2 text-xs text-gray-600">
                      <span>{formatFileSize(material.fileSize)}</span>
                      {material.status === 'uploading' && (
                        <>
                          <span>•</span>
                          <span className="animate-pulse">Cargando...</span>
                        </>
                      )}
                      {material.status === 'uploaded' && (
                        <>
                          <span>•</span>
                          <span className="text-green-600">✓ Cargado</span>
                        </>
                      )}
                      {material.status === 'error' && (
                        <>
                          <span>•</span>
                          <span className="text-red-600">
                            {material.errorMessage}
                          </span>
                        </>
                      )}
                    </div>
                  </div>
                </div>
                <button
                  onClick={() => onRemoveMaterial(material.id)}
                  className="ml-2 p-1 hover:bg-gray-200 rounded transition-colors"
                  title="Eliminar archivo"
                >
                  <X size={18} className="text-gray-600" />
                </button>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};

export default MaterialUploader;
