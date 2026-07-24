import { useCallback, useRef, useState } from 'react';
import { UploadCloud, FileText, X } from 'lucide-react';

interface FileDropzoneProps {
  file: File | null;
  onFileSelected: (file: File | null) => void;
  accept?: string;
  title?: string;
  subtitle?: string;
}

export default function FileDropzone({
  file,
  onFileSelected,
  accept = 'application/pdf',
  title = 'Arrastra tu PDF aquí',
  subtitle = 'o haz clic para seleccionar un archivo',
}: FileDropzoneProps) {
  const [isDragging, setIsDragging] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const handleFiles = useCallback(
    (fileList: FileList | null) => {
      const picked = fileList?.[0] ?? null;
      onFileSelected(picked);
    },
    [onFileSelected]
  );

  if (file) {
    return (
      <div className="flex items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.04] p-4">
        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-brand-500/20">
          <FileText className="h-5 w-5 text-brand-300" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="truncate text-sm font-medium text-white">{file.name}</div>
          <div className="text-xs text-slate-400">{(file.size / 1024 / 1024).toFixed(2)} MB</div>
        </div>
        <button
          type="button"
          onClick={() => onFileSelected(null)}
          className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-slate-400 hover:bg-white/10 hover:text-white"
        >
          <X className="h-4 w-4" />
        </button>
      </div>
    );
  }

  return (
    <div
      onDragOver={(e) => {
        e.preventDefault();
        setIsDragging(true);
      }}
      onDragLeave={() => setIsDragging(false)}
      onDrop={(e) => {
        e.preventDefault();
        setIsDragging(false);
        handleFiles(e.dataTransfer.files);
      }}
      onClick={() => inputRef.current?.click()}
      className={`flex cursor-pointer flex-col items-center gap-3 rounded-2xl border-2 border-dashed p-10 text-center transition-colors ${
        isDragging ? 'border-brand-400 bg-brand-500/10' : 'border-white/15 bg-white/[0.02] hover:border-white/25'
      }`}
    >
      <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-white/5">
        <UploadCloud className="h-7 w-7 text-slate-400" />
      </div>
      <div>
        <p className="font-medium text-white">{title}</p>
        <p className="mt-1 text-sm text-slate-400">{subtitle}</p>
      </div>
      <input
        ref={inputRef}
        type="file"
        accept={accept}
        className="hidden"
        onChange={(e) => handleFiles(e.target.files)}
      />
    </div>
  );
}
