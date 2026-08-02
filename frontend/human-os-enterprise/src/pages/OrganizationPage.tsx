import { useMemo, useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { Plus, ChevronRight, ChevronDown, Building2, Trash2, Pencil, Sparkles, Loader2 } from 'lucide-react';
import { clsx } from 'clsx';
import { useAuth } from '@/auth/AuthContext';
import {
  createDepartment,
  deleteDepartment,
  extractOrgChart,
  getDepartments,
  updateDepartment,
  type DepartmentDto,
  type SaveDepartmentRequest,
} from '@/api/orgStructureApi';

interface DepartmentNode extends DepartmentDto {
  children: DepartmentNode[];
}

function buildTree(departments: DepartmentDto[]): DepartmentNode[] {
  const byId = new Map<string, DepartmentNode>(departments.map((d) => [d.departmentId, { ...d, children: [] }]));
  const roots: DepartmentNode[] = [];

  for (const node of byId.values()) {
    if (node.parentDepartmentId && byId.has(node.parentDepartmentId)) {
      byId.get(node.parentDepartmentId)!.children.push(node);
    } else {
      roots.push(node);
    }
  }

  return roots;
}

export function OrganizationPage() {
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [importError, setImportError] = useState<string | null>(null);

  const extractMutation = useMutation({
    mutationFn: (file: File) => extractOrgChart(tenantId, file),
    onSuccess: (result) => navigate('/organizacion/importar', { state: { result } }),
    onError: (error) => setImportError(error instanceof Error ? error.message : 'Ocurrió un error inesperado.'),
  });

  function handleOrgChartFileSelected(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    setImportError(null);
    extractMutation.mutate(file);
  }

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<DepartmentDto | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [parentDepartmentId, setParentDepartmentId] = useState<string>('');

  const departmentsQuery = useQuery({
    queryKey: ['departments', tenantId],
    queryFn: () => getDepartments(tenantId),
  });

  const tree = useMemo(() => buildTree(departmentsQuery.data ?? []), [departmentsQuery.data]);

  function invalidate() {
    void queryClient.invalidateQueries({ queryKey: ['departments', tenantId] });
  }

  const createMutation = useMutation({
    mutationFn: (body: SaveDepartmentRequest) => createDepartment(tenantId, body),
    onSuccess: () => {
      invalidate();
      closeForm();
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, body }: { id: string; body: SaveDepartmentRequest }) => updateDepartment(id, body),
    onSuccess: () => {
      invalidate();
      closeForm();
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteDepartment(id),
    onSuccess: invalidate,
  });

  function openCreateForm(parentId?: string) {
    setEditing(null);
    setName('');
    setDescription('');
    setParentDepartmentId(parentId ?? '');
    setFormOpen(true);
  }

  function openEditForm(department: DepartmentDto) {
    setEditing(department);
    setName(department.name);
    setDescription(department.description ?? '');
    setParentDepartmentId(department.parentDepartmentId ?? '');
    setFormOpen(true);
  }

  function closeForm() {
    setFormOpen(false);
    setEditing(null);
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const body: SaveDepartmentRequest = {
      name,
      description: description || null,
      parentDepartmentId: parentDepartmentId || null,
    };

    if (editing) {
      updateMutation.mutate({ id: editing.departmentId, body });
    } else {
      createMutation.mutate(body);
    }
  }

  const isSaving = createMutation.isPending || updateMutation.isPending;
  const mutationError = createMutation.error ?? updateMutation.error ?? deleteMutation.error;

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">Organización</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
            Áreas, departamentos y equipos de {user?.tenantName}.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <input
            ref={fileInputRef}
            type="file"
            accept="image/*"
            className="hidden"
            onChange={handleOrgChartFileSelected}
          />
          <button
            type="button"
            disabled={extractMutation.isPending}
            onClick={() => fileInputRef.current?.click()}
            className="inline-flex items-center gap-2 rounded-full border border-violet-200 bg-white px-4 py-2.5 text-sm font-medium text-violet-700 shadow-sm transition hover:bg-violet-50 disabled:opacity-50 dark:border-violet-500/30 dark:bg-white/5 dark:text-violet-300 dark:hover:bg-violet-500/10"
          >
            {extractMutation.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Sparkles className="h-4 w-4" />
            )}
            {extractMutation.isPending ? 'Analizando imagen…' : 'Importar organigrama (IA)'}
          </button>
          <button
            type="button"
            onClick={() => openCreateForm()}
            className="inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-violet-600 to-violet-500 px-4 py-2.5 text-sm font-medium text-white shadow-md shadow-violet-500/30 transition hover:brightness-110"
          >
            <Plus className="h-4 w-4" />
            Nuevo departamento
          </button>
        </div>
      </div>

      {importError && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {importError}
        </div>
      )}

      {mutationError && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {mutationError instanceof Error ? mutationError.message : 'Ocurrió un error inesperado.'}
        </div>
      )}

      {formOpen && (
        <form
          onSubmit={handleSubmit}
          className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5"
        >
          <h2 className="text-sm font-semibold text-slate-900 dark:text-white">
            {editing ? 'Editar departamento' : 'Nuevo departamento'}
          </h2>
          <div>
            <label className="block text-xs font-medium text-slate-500 dark:text-white/50">Nombre</label>
            <input
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="p. ej. Ventas, Ingeniería, Recursos Humanos"
              className="mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-500 dark:text-white/50">
              Descripción (opcional)
            </label>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={2}
              className="mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-500 dark:text-white/50">
              Departamento padre (opcional)
            </label>
            <select
              value={parentDepartmentId}
              onChange={(e) => setParentDepartmentId(e.target.value)}
              className="mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
            >
              <option value="">— Nivel superior (Área) —</option>
              {departmentsQuery.data
                ?.filter((d) => d.departmentId !== editing?.departmentId)
                .map((d) => (
                  <option key={d.departmentId} value={d.departmentId}>
                    {d.name}
                  </option>
                ))}
            </select>
          </div>
          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={closeForm}
              className="rounded-full px-4 py-2 text-sm font-medium text-slate-500 hover:bg-slate-100 dark:text-white/50 dark:hover:bg-white/10"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={isSaving}
              className="rounded-full bg-brand-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-brand-700 disabled:opacity-60"
            >
              {isSaving ? 'Guardando…' : 'Guardar'}
            </button>
          </div>
        </form>
      )}

      <div className="rounded-2xl border border-slate-200 bg-white p-2 dark:border-white/10 dark:bg-white/5">
        {departmentsQuery.isLoading && <p className="p-4 text-sm text-slate-400">Cargando…</p>}
        {departmentsQuery.isSuccess && tree.length === 0 && (
          <div className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-violet-200 bg-violet-50/50 p-8 text-center dark:border-violet-500/20 dark:bg-violet-500/5">
            <span className="flex h-12 w-12 items-center justify-center rounded-full bg-violet-100 text-violet-600 dark:bg-violet-500/15 dark:text-violet-300">
              <Building2 className="h-6 w-6" />
            </span>
            <p className="text-sm text-slate-500 dark:text-white/40">
              Aún no hay departamentos. Crea el primero para empezar a organizar tu empresa.
            </p>
          </div>
        )}
        {tree.map((node) => (
          <DepartmentRow
            key={node.departmentId}
            node={node}
            depth={0}
            onAddChild={openCreateForm}
            onEdit={openEditForm}
            onDelete={(id) => deleteMutation.mutate(id)}
          />
        ))}
      </div>
    </div>
  );
}

function DepartmentRow({
  node,
  depth,
  onAddChild,
  onEdit,
  onDelete,
}: {
  node: DepartmentNode;
  depth: number;
  onAddChild: (parentId: string) => void;
  onEdit: (department: DepartmentDto) => void;
  onDelete: (id: string) => void;
}) {
  const [expanded, setExpanded] = useState(true);
  const hasChildren = node.children.length > 0;

  return (
    <div>
      <div
        className="group flex items-center gap-2 rounded-xl px-2 py-2 hover:bg-slate-50 dark:hover:bg-white/5"
        style={{ paddingLeft: `${depth * 20 + 8}px` }}
      >
        <button
          type="button"
          onClick={() => setExpanded((v) => !v)}
          className={clsx('flex h-5 w-5 items-center justify-center text-slate-400', !hasChildren && 'invisible')}
        >
          {expanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
        </button>
        <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-violet-100 text-violet-600 dark:bg-violet-500/15 dark:text-violet-300">
          <Building2 className="h-3.5 w-3.5" />
        </span>
        <span className="flex-1 text-sm font-medium text-slate-800 dark:text-white">{node.name}</span>
        <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-500 dark:bg-white/10 dark:text-white/40">
          {node.jobRoleCount} roles · {node.personCount} personas
        </span>
        <div className="flex items-center gap-1 opacity-0 transition group-hover:opacity-100">
          <button
            type="button"
            title="Agregar sub-departamento"
            onClick={() => onAddChild(node.departmentId)}
            className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-slate-200 dark:hover:bg-white/10"
          >
            <Plus className="h-3.5 w-3.5" />
          </button>
          <button
            type="button"
            title="Editar"
            onClick={() => onEdit(node)}
            className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-slate-200 dark:hover:bg-white/10"
          >
            <Pencil className="h-3.5 w-3.5" />
          </button>
          <button
            type="button"
            title="Desactivar"
            onClick={() => onDelete(node.departmentId)}
            className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-red-100 hover:text-red-600 dark:hover:bg-red-500/10"
          >
            <Trash2 className="h-3.5 w-3.5" />
          </button>
        </div>
      </div>
      {expanded &&
        node.children.map((child) => (
          <DepartmentRow key={child.departmentId} node={child} depth={depth + 1} onAddChild={onAddChild} onEdit={onEdit} onDelete={onDelete} />
        ))}
    </div>
  );
}
