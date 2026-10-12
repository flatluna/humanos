import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import {
  Layers3,
  Sparkles,
  GraduationCap,
  Database,
  Table,
  Pencil,
} from 'lucide-react';
import { familySkillsStore, CurriculumSkill } from './familySkillsStore';

interface StageDef {
  key: string;
  nameEs: string;
  nameEn: string;
  age: string;
  group: string;
  icon: string;
  descEs: string;
  descEn: string;
}

const STAGES: StageDef[] = [
  {
    key: 'kindergarten',
    nameEs: 'Kindergarten (Preescolar)',
    nameEn: 'Kindergarten',
    age: '5 - 6 yrs',
    group: 'Elementary',
    icon: '🎒',
    descEs: 'Fundamentos de lectoescritura bilingüe, conteo del 1 al 20, autorregulación y curiosidad científica.',
    descEn: 'Early bilingual phonics, number sense 1-20, classroom self-regulation, and natural curiosity.',
  },
  {
    key: '1st_2nd_grade',
    nameEs: '1° & 2° de Primaria',
    nameEn: '1st & 2nd Grade',
    age: '6 - 8 yrs',
    group: 'Elementary',
    icon: '📚',
    descEs: 'Lectura autónoma, sumas y restas con llevada, oraciones completas y hábitos de tareas escolares.',
    descEn: 'Independent reading, addition/subtraction regrouping, complete sentences, and homework habits.',
  },
  {
    key: '3rd_5th_grade',
    nameEs: '3° a 5° de Primaria',
    nameEn: '3rd - 5th Grade',
    age: '8 - 11 yrs',
    group: 'Elementary',
    icon: '🔬',
    descEs: 'Tablas de multiplicar, comprensión de textos largos, fracciones, método científico y Scratch coding.',
    descEn: 'Multiplication fluency, multi-paragraph essays, fractions, scientific method, and block coding.',
  },
  {
    key: 'middle_school',
    nameEs: 'Secundaria (Middle School 6°-8°)',
    nameEn: 'Middle School (6th - 8th)',
    age: '11 - 14 yrs',
    group: 'Middle',
    icon: '⚡',
    descEs: 'Pre-álgebra, debate bilingüe, programación en Python/Robótica FLL y gestión del tiempo personal.',
    descEn: 'Pre-algebra, structured debate, Python robotics, and independent time management.',
  },
  {
    key: 'high_school',
    nameEs: 'Preparatoria / High School (9°-12°)',
    nameEn: 'High School (9th - 12th)',
    age: '14 - 18 yrs',
    group: 'HighSchool',
    icon: '🎓',
    descEs: 'Cálculo, preparación SAT/ACT, materias AP/IB, portafolio extracurricular y admisiones universitarias.',
    descEn: 'Calculus, SAT/ACT test prep, AP/IB courses, extracurricular portfolio, and college admissions.',
  },
  {
    key: 'college',
    nameEs: 'Universidad & Pre-Grado',
    nameEn: 'College & Undergraduate',
    age: '18 - 23 yrs',
    group: 'College',
    icon: '🏛️',
    descEs: 'Especialización profesional, investigación rigurosa, pasantías y autonomía económica.',
    descEn: 'Major specialization, empirical research, professional internships, and financial autonomy.',
  },
  {
    key: 'professional',
    nameEs: 'Profesional & Adulto',
    nameEn: 'Professional Adulthood',
    age: '24+ yrs',
    group: 'Professional',
    icon: '💼',
    descEs: 'Estrategia de IA, liderazgo de equipos globales, patrimonio familiar y balance carrera-familia.',
    descEn: 'Executive AI strategy, global leadership, family wealth planning, and work-life harmony.',
  },
];

export function FamilySkillsAdminPage({ isEnglish }: { isEnglish: boolean }) {
  const [selectedStage, setSelectedStage] = useState<string>('kindergarten');
  const [savedSkills, setSavedSkills] = useState<CurriculumSkill[]>([]);
  const [activeTab, setActiveTab] = useState<'catalog' | 'tables'>('catalog');
  const [modalCourseCap, setModalCourseCap] = useState<CurriculumSkill | null>(null);

  const currentStageDef = STAGES.find((s) => s.key === selectedStage) || STAGES[0];

  // Read saved skills dynamically whenever selectedStage changes
  useEffect(() => {
    const skills = familySkillsStore.getSavedSkills(selectedStage);
    setSavedSkills(skills);
  }, [selectedStage]);

  const hasSkills = savedSkills.length > 0;

  return (
    <div className="family-skills-admin-page" style={{ display: 'grid', gap: '28px' }}>
      <style>{`
        a {
          text-decoration: none;
          color: #464feb;
        }
        .admin-curriculum-table {
          width: 100%;
          border-collapse: collapse;
          font-size: 13px;
          background: #ffffff;
          border-radius: 12px;
          overflow: hidden;
        }
        .admin-curriculum-table th, .admin-curriculum-table td {
          border: 1px solid #e6e6e6;
          padding: 12px 14px;
          text-align: left;
          vertical-align: top;
        }
        .admin-curriculum-table th {
          background-color: #f5f5f5;
          font-family: 'Space Grotesk', sans-serif;
          font-size: 12px;
          font-weight: 700;
          color: #1d3557;
          letter-spacing: .02em;
        }
        .admin-curriculum-table tr:hover td {
          background-color: #fafcff;
        }
      `}</style>

      {/* Top Header */}
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '20px', flexWrap: 'wrap' }}>
        <div>
          <div style={{ display: 'inline-flex', alignItems: 'center', gap: '8px', padding: '4px 12px', background: '#e0ebff', borderRadius: '999px', color: '#1e59b8', fontSize: '11px', fontWeight: '700', textTransform: 'uppercase', letterSpacing: '.05em', marginBottom: '8px' }}>
            <Layers3 size={14} />
            <span>{isEnglish ? 'Global Reference Catalog' : 'Catálogo Global de Referencia'}</span>
          </div>
          <h2 style={{ margin: '0 0 6px', color: '#1d3557', fontSize: '28px' }}>
            {isEnglish ? 'Family Skills by Human Development Stages' : 'Family Skills por Niveles en el Tiempo Humano'}
          </h2>
          <p style={{ margin: 0, color: '#55708f', fontSize: '13px', maxWidth: '780px' }}>
            {isEnglish
              ? 'Official capabilities across life stages. No mock data: displays only verified saved skills from the database or allows AI generation for each grade level.'
              : 'Capabilities oficiales por etapas de vida. Sin tarjetas inventadas: muestra únicamente los skills guardados en base de datos o permite generarlos con AI por nivel escolar.'}
          </p>
        </div>

        {/* View Toggle */}
        <div style={{ display: 'flex', gap: '6px', background: '#eef4fb', padding: '4px', borderRadius: '12px' }}>
          <button
            type="button"
            onClick={() => setActiveTab('catalog')}
            style={{ padding: '8px 14px', border: 0, borderRadius: '8px', cursor: 'pointer', background: activeTab === 'catalog' ? '#1f5bb7' : 'transparent', color: activeTab === 'catalog' ? '#ffffff' : '#55708f', font: '600 12px Space Grotesk, sans-serif' }}
          >
            {isEnglish ? 'Skills by Stage' : 'Skills por Etapa'}
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('tables')}
            style={{ padding: '8px 14px', border: 0, borderRadius: '8px', cursor: 'pointer', background: activeTab === 'tables' ? '#1f5bb7' : 'transparent', color: activeTab === 'tables' ? '#ffffff' : '#55708f', font: '600 12px Space Grotesk, sans-serif', display: 'flex', alignItems: 'center', gap: '6px' }}
          >
            <Table size={14} />
            <span>{isEnglish ? 'Database Tables' : 'Tablas de BD (family.*)'}</span>
          </button>
        </div>
      </div>

      {activeTab === 'catalog' && (
        <>
          {/* Human Stages Selector */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: '12px' }}>
            {STAGES.map((s) => {
              const isSelected = selectedStage === s.key;
              const stageHasSkills = familySkillsStore.hasSavedSkills(s.key);

              return (
                <button
                  key={s.key}
                  type="button"
                  onClick={() => setSelectedStage(s.key)}
                  style={{
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'flex-start',
                    justifyContent: 'space-between',
                    padding: '16px 14px',
                    borderRadius: '14px',
                    border: isSelected ? '2px solid #1f5bb7' : '1px solid #dce6f2',
                    background: isSelected ? '#ffffff' : '#f8fafd',
                    cursor: 'pointer',
                    boxShadow: isSelected ? '0 6px 16px rgba(31, 91, 183, 0.15)' : 'none',
                    textAlign: 'left',
                    transition: 'all .15s ease',
                  }}
                >
                  <div>
                    <span style={{ fontSize: '24px', display: 'block', marginBottom: '8px' }}>{s.icon}</span>
                    <strong style={{ fontSize: '13px', color: isSelected ? '#1f5bb7' : '#1d3557', display: 'block', lineHeight: '1.2' }}>
                      {isEnglish ? s.nameEn : s.nameEs}
                    </strong>
                  </div>
                  <div style={{ marginTop: '12px', borderTop: '1px solid #eef3f9', paddingTop: '8px', width: '100%', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '11px', color: '#7b8da5', fontWeight: '600' }}>{s.age}</span>
                    {stageHasSkills && (
                      <span style={{ fontSize: '10px', color: '#047857', fontWeight: '700', background: '#e0f7ef', padding: '1px 6px', borderRadius: '4px' }}>
                        ● Guardado
                      </span>
                    )}
                  </div>
                </button>
              );
            })}
          </div>

          {/* Active Stage Highlight Banner */}
          <div style={{ padding: '20px 24px', background: '#ffffff', border: '1px solid #dce6f2', borderRadius: '16px', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '20px', flexWrap: 'wrap' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
              <span style={{ fontSize: '32px' }}>{currentStageDef.icon}</span>
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                  <h3 style={{ margin: 0, fontSize: '18px', color: '#1d3557' }}>
                    {isEnglish ? currentStageDef.nameEn : currentStageDef.nameEs}
                  </h3>
                  <span style={{ padding: '3px 8px', background: hasSkills ? '#e0f7ef' : '#f1f5f9', color: hasSkills ? '#047857' : '#64748b', borderRadius: '6px', fontSize: '11px', fontWeight: '700' }}>
                    {hasSkills
                      ? (isEnglish ? `● ${savedSkills.length} Saved Skills (Active)` : `● ${savedSkills.length} Skills Guardados (Activo)`)
                      : (isEnglish ? '○ No skills saved yet' : '○ Sin skills guardados aún')}
                  </span>
                </div>
                <p style={{ margin: '4px 0 0', color: '#55708f', fontSize: '13px' }}>
                  {isEnglish ? currentStageDef.descEn : currentStageDef.descEs}
                </p>
              </div>
            </div>

            {/* Actions: Edit if exists, or Generate if empty */}
            {hasSkills ? (
              <Link
                to={`/family-skills/wizard?stage=${selectedStage}`}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  padding: '10px 18px',
                  background: '#1d3557',
                  color: '#ffffff',
                  border: 0,
                  borderRadius: '10px',
                  font: '700 12px Space Grotesk, sans-serif',
                  cursor: 'pointer',
                  boxShadow: '0 4px 12px rgba(29, 53, 87, 0.25)',
                  textDecoration: 'none',
                }}
              >
                <Pencil size={15} />
                <span>
                  {isEnglish ? 'Manage & Edit Skills' : 'Gestionar & Editar Skills'}
                </span>
              </Link>
            ) : (
              <Link
                to={`/family-skills/wizard?stage=${selectedStage}`}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  padding: '10px 18px',
                  background: 'linear-gradient(135deg, #1f5bb7 0%, #3b82f6 100%)',
                  color: '#ffffff',
                  border: 0,
                  borderRadius: '10px',
                  font: '700 12px Space Grotesk, sans-serif',
                  cursor: 'pointer',
                  boxShadow: '0 4px 12px rgba(31, 91, 183, 0.25)',
                  textDecoration: 'none',
                }}
              >
                <Sparkles size={16} />
                <span>
                  {isEnglish ? 'Generate Skills with AI' : 'Generar Skills con AI'}
                </span>
              </Link>
            )}
          </div>

          {/* CONTENT: Either real saved table, or Clean Empty State (NO INVENTED CARDS!) */}
          {hasSkills ? (
            <div style={{ background: '#ffffff', border: '1px solid #e6e6e6', borderRadius: '14px', overflowX: 'auto', boxShadow: '0 2px 8px rgba(0,0,0,0.02)' }}>
              <table className="admin-curriculum-table">
                <thead>
                  <tr>
                    <th style={{ width: '180px' }}>{isEnglish ? 'Subject / Area' : 'Materia / Asignatura'}</th>
                    <th style={{ width: '110px' }}>{isEnglish ? 'Code' : 'Código'}</th>
                    <th style={{ width: '280px' }}>{isEnglish ? 'Skill / Capability' : 'Competencia / Skill'}</th>
                    <th>{isEnglish ? 'Why it matters & Description' : 'Descripción & Por qué es clave'}</th>
                    <th style={{ width: '260px' }}>{isEnglish ? 'Micro-Skills Covered' : 'Competencias Cubiertas'}</th>
                    <th style={{ width: '130px', textAlign: 'center' }}>{isEnglish ? 'Action' : 'Acción'}</th>
                  </tr>
                </thead>
                <tbody>
                  {savedSkills.map((skill) => (
                    <tr key={skill.id}>
                      <td>
                        <span style={{ display: 'inline-block', padding: '3px 8px', background: '#eef2ff', color: '#464feb', borderRadius: '6px', fontWeight: '700', fontSize: '11px', marginBottom: '4px' }}>
                          {isEnglish ? skill.subjectNameEn : skill.subjectNameEs}
                        </span>
                        <span style={{ display: 'block', fontSize: '10px', color: '#7b8da5' }}>
                          ⏱️ ~{skill.hoursToMaster} hrs maestría
                        </span>
                      </td>

                      <td>
                        <span style={{ font: '700 11px Space Grotesk, sans-serif', color: '#1d3557', background: '#f5f5f5', padding: '3px 7px', borderRadius: '5px' }}>
                          {skill.code}
                        </span>
                      </td>

                      <td>
                        <strong style={{ display: 'block', fontSize: '13px', color: '#1d3557', marginBottom: '4px' }}>
                          {isEnglish ? skill.nameEn : skill.nameEs}
                        </strong>
                        <span style={{ fontSize: '11px', color: '#55708f', lineHeight: '1.4', display: 'block' }}>
                          {isEnglish ? skill.descriptionEn : skill.descriptionEs}
                        </span>
                      </td>

                      <td>
                        <div style={{ background: '#f8fafd', border: '1px solid #eef3f9', borderRadius: '8px', padding: '8px 10px' }}>
                          <span style={{ display: 'block', fontSize: '10px', textTransform: 'uppercase', letterSpacing: '.05em', color: '#7b8da5', fontWeight: '700', marginBottom: '2px' }}>
                            💡 {isEnglish ? 'Why it matters:' : 'Por qué es clave:'}
                          </span>
                          <p style={{ margin: 0, fontSize: '11px', color: '#1d3557', lineHeight: '1.4' }}>
                            {isEnglish ? skill.whyItMattersEn : skill.whyItMattersEs}
                          </p>
                        </div>
                      </td>

                      <td>
                        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px' }}>
                          {skill.skillsCovered.map((m, idx) => (
                            <span key={idx} style={{ padding: '2px 7px', background: '#f5f5f5', borderRadius: '5px', fontSize: '10px', color: '#334155', fontWeight: '600' }}>
                              ✓ {m}
                            </span>
                          ))}
                        </div>
                      </td>

                      <td style={{ textAlign: 'center', verticalAlign: 'middle' }}>
                        <button
                          type="button"
                          onClick={() => {
                            const returnTo = `/new-academic-learning?code=${encodeURIComponent(skill.code)}&stage=kindergarten`;
                            const launchUrl = `http://localhost:7071/api/humanos/studio-launch?oid=6a1e42be-4cdc-4f24-b792-d5f7ef0bcf06&tid=0e9c8663-a4ff-440e-af94-be25e63a1a6a&email=jluna%40flatbit.com&returnTo=${encodeURIComponent(returnTo)}`;
                            window.open(launchUrl, '_blank');
                          }}
                          style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '5px',
                            padding: '6px 12px',
                            background: '#464feb',
                            color: '#ffffff',
                            border: 0,
                            borderRadius: '6px',
                            font: '700 11px Space Grotesk, sans-serif',
                            cursor: 'pointer',
                            whiteSpace: 'nowrap',
                          }}
                        >
                          <GraduationCap size={13} />
                          <span>{isEnglish ? 'Crear Curso' : 'Crear Curso'}</span>
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            /* CLEAN EMPTY STATE - ZERO INVENTED CARDS */
            <div style={{ background: '#ffffff', border: '2px dashed #dce6f2', borderRadius: '18px', padding: '56px 24px', textAlign: 'center', display: 'grid', placeItems: 'center', gap: '14px' }}>
              <div style={{ width: '60px', height: '60px', borderRadius: '18px', background: '#f0f6ff', display: 'grid', placeItems: 'center', color: '#1f5bb7' }}>
                <Layers3 size={30} />
              </div>
              <h3 style={{ margin: 0, fontSize: '19px', color: '#1d3557' }}>
                {isEnglish
                  ? `No skills saved yet for ${currentStageDef.nameEn}`
                  : `No hay skills guardados aún para ${currentStageDef.nameEs}`}
              </h3>
              <p style={{ margin: 0, fontSize: '13px', color: '#55708f', maxWidth: '520px', lineHeight: '1.5' }}>
                {isEnglish
                  ? `This stage (${currentStageDef.age}) currently has 0 capabilities in database. Click below to launch the AI Curriculum Wizard and formulate standard skills across the 11 core subjects.`
                  : `Esta etapa (${currentStageDef.age}) tiene actualmente 0 capabilities en base de datos. Haz clic abajo para abrir el Wizard y formular los skills oficiales en las 11 materias escolares.`}
              </p>
              <Link
                to={`/family-skills/wizard?stage=${selectedStage}`}
                style={{
                  padding: '11px 24px',
                  background: 'linear-gradient(135deg, #1f5bb7 0%, #3b82f6 100%)',
                  color: '#ffffff',
                  border: 0,
                  borderRadius: '10px',
                  font: '700 13px Space Grotesk, sans-serif',
                  textDecoration: 'none',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  boxShadow: '0 4px 12px rgba(31, 91, 183, 0.25)',
                }}
              >
                <Sparkles size={16} />
                <span>{isEnglish ? 'Generate Skills with AI' : 'Generar Skills con AI'}</span>
              </Link>
            </div>
          )}
        </>
      )}

      {/* Database Tables Inspector Tab */}
      {activeTab === 'tables' && (
        <div style={{ display: 'grid', gap: '20px' }}>
          <div style={{ padding: '20px 24px', background: '#ffffff', border: '1px solid #dce6f2', borderRadius: '16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '10px' }}>
              <Database size={20} color="#1f5bb7" />
              <h3 style={{ margin: 0, fontSize: '18px', color: '#1d3557' }}>
                {isEnglish ? 'Database Schema Architecture (Isolated family.* Schema)' : 'Arquitectura de Base de Datos (Esquema Aislado family.*)'}
              </h3>
            </div>
            <p style={{ margin: 0, color: '#55708f', fontSize: '13px' }}>
              {isEnglish
                ? 'These 4 tables store human life stages, reference capabilities, many-to-many grade requirements, and AI-generated course content. Completely additive, touching 0 existing corporate tables.'
                : 'Estas 4 tablas almacenan las etapas de vida humana, capabilities de referencia, requisitos por grado escolar y el contenido de cursos generado por AI. Totalmente aditivas, sin tocar tablas corporativas existentes.'}
            </p>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '16px' }}>
            <div style={{ background: '#ffffff', border: '1px solid #dce6f2', borderRadius: '14px', padding: '18px' }}>
              <strong style={{ display: 'block', fontSize: '14px', color: '#1d3557', marginBottom: '6px' }}>
                1. family.HumanDevelopmentStages
              </strong>
              <p style={{ fontSize: '12px', color: '#55708f', margin: '0 0 10px' }}>
                {isEnglish ? 'Stages in human time (Kindergarten, 1st Grade, High School, Adulthood).' : 'Etapas en el tiempo humano (Kindergarten, 1° Primaria, High School, Adulto).'}
              </p>
              <pre style={{ margin: 0, padding: '10px', background: '#f8fafd', borderRadius: '8px', fontSize: '11px', color: '#334155', overflowX: 'auto' }}>
{`StageId UNIQUEIDENTIFIER (PK)
StageKey NVARCHAR(50) -- 'kindergarten'
StageNameEs / StageNameEn
AgeRange NVARCHAR(50) -- '5 - 6 yrs'
CategoryGroup NVARCHAR(50) -- 'Elementary'
DisplayOrder INT`}
              </pre>
            </div>

            <div style={{ background: '#ffffff', border: '1px solid #dce6f2', borderRadius: '14px', padding: '18px' }}>
              <strong style={{ display: 'block', fontSize: '14px', color: '#1d3557', marginBottom: '6px' }}>
                2. family.HumanCapabilities
              </strong>
              <p style={{ fontSize: '12px', color: '#55708f', margin: '0 0 10px' }}>
                {isEnglish ? 'Global and custom capabilities catalog.' : 'Catálogo de capabilities globales y personalizadas.'}
              </p>
              <pre style={{ margin: 0, padding: '10px', background: '#f8fafd', borderRadius: '8px', fontSize: '11px', color: '#334155', overflowX: 'auto' }}>
{`CapabilityId UNIQUEIDENTIFIER (PK)
Code NVARCHAR(50) -- 'KIND-LIT-01'
NameEs / NameEn NVARCHAR(150)
Domain NVARCHAR(50) -- 'STEM & Math'
FamilyId UNIQUEIDENTIFIER NULL -- NULL = Global
WhyItMattersEs / WhyItMattersEn`}
              </pre>
            </div>

            <div style={{ background: '#ffffff', border: '1px solid #dce6f2', borderRadius: '14px', padding: '18px' }}>
              <strong style={{ display: 'block', fontSize: '14px', color: '#1d3557', marginBottom: '6px' }}>
                3. family.StageCapabilityRequirements
              </strong>
              <p style={{ fontSize: '12px', color: '#55708f', margin: '0 0 10px' }}>
                {isEnglish ? 'Stage ↔ Capability requirement mapping (Many-to-Many).' : 'Asociación Nivel Humano ↔ Capabilities Requeridas para Maestría.'}
              </p>
              <pre style={{ margin: 0, padding: '10px', background: '#f8fafd', borderRadius: '8px', fontSize: '11px', color: '#334155', overflowX: 'auto' }}>
{`RequirementId UNIQUEIDENTIFIER (PK)
StageId FK -> HumanDevelopmentStages
CapabilityId FK -> HumanCapabilities
TargetMasteryLevel -- 'Foundation' / 'Master'
RecommendedHoursToMaster INT
IsMandatoryForNextStage BIT`}
              </pre>
            </div>

            <div style={{ background: '#ffffff', border: '1px solid #dce6f2', borderRadius: '14px', padding: '18px' }}>
              <strong style={{ display: 'block', fontSize: '14px', color: '#1d3557', marginBottom: '6px' }}>
                4. family.CapabilityLearningContent
              </strong>
              <p style={{ fontSize: '12px', color: '#55708f', margin: '0 0 10px' }}>
                {isEnglish ? 'AI-generated pedagogical courses, stories & homework guides.' : 'Contenido pedagógico y cursos generados por AI.'}
              </p>
              <pre style={{ margin: 0, padding: '10px', background: '#f8fafd', borderRadius: '8px', fontSize: '11px', color: '#334155', overflowX: 'auto' }}>
{`ContentId UNIQUEIDENTIFIER (PK)
CapabilityId FK -> HumanCapabilities
StageId FK -> HumanDevelopmentStages
KidExplanationStory NVARCHAR(MAX)
ParentGuide NVARCHAR(MAX)
HomeActivitiesJson NVARCHAR(MAX)
SocraticQuestionsJson NVARCHAR(MAX)`}
              </pre>
            </div>
          </div>
        </div>
      )}

      {/* HumanOS Course Generator Modal */}
      {modalCourseCap && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)', display: 'grid', placeItems: 'center', zIndex: 100, padding: '20px' }}>
          <div style={{ background: '#ffffff', borderRadius: '20px', padding: '28px', maxWidth: '580px', width: '100%', boxShadow: '0 20px 50px rgba(0,0,0,0.3)', display: 'grid', gap: '18px' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span style={{ fontSize: '20px' }}>🎓</span>
                <strong style={{ fontSize: '16px', color: '#1d3557' }}>
                  {isEnglish ? 'Generate Course in HumanOS' : 'Generar Curso en HumanOS'}
                </strong>
              </div>
              <button
                type="button"
                onClick={() => setModalCourseCap(null)}
                style={{ border: 0, background: 'transparent', cursor: 'pointer', fontSize: '16px', color: '#7b8da5' }}
              >
                ✕
              </button>
            </div>

            <div style={{ padding: '14px 18px', background: '#f8fafd', borderRadius: '12px', border: '1px solid #dce6f2' }}>
              <span style={{ fontSize: '11px', color: '#7b8da5', fontWeight: '700', textTransform: 'uppercase' }}>
                {modalCourseCap.code} · {modalCourseCap.subjectNameEs}
              </span>
              <h4 style={{ margin: '4px 0 6px', fontSize: '16px', color: '#1d3557' }}>
                {isEnglish ? modalCourseCap.nameEn : modalCourseCap.nameEs}
              </h4>
              <p style={{ margin: 0, fontSize: '12px', color: '#55708f' }}>
                {isEnglish ? modalCourseCap.descriptionEn : modalCourseCap.descriptionEs}
              </p>
            </div>

            <div style={{ padding: '12px 16px', background: '#eef4fb', borderRadius: '12px', fontSize: '12px', color: '#1e59b8' }}>
              💡 {isEnglish
                ? 'This capability blueprint will be sent to the HumanOS Course Generation Pipeline (Capability Studio) with kid-friendly story analogies, parent guides, and interactive quizzes.'
                : 'Esta ficha de capability se enviará al Pipeline de Cursos de HumanOS (Capability Studio) con cuentos infantiles, guías para padres y evaluaciones interactivas.'}
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '10px' }}>
              <button
                type="button"
                onClick={() => setModalCourseCap(null)}
                style={{ padding: '9px 16px', border: '1px solid #dce6f2', borderRadius: '8px', background: '#ffffff', color: '#55708f', font: '600 12px Space Grotesk, sans-serif', cursor: 'pointer' }}
              >
                {isEnglish ? 'Cancel' : 'Cancelar'}
              </button>
              <button
                type="button"
                onClick={() => {
                  alert(
                    isEnglish
                      ? `Ready! When integrated, this will launch HumanOS Course Generation for: "${modalCourseCap.nameEn}".`
                      : `¡Listo! Al integrar, esto lanzará el generador de cursos de HumanOS para: "${modalCourseCap.nameEs}".`
                  );
                  setModalCourseCap(null);
                }}
                style={{ padding: '9px 20px', border: 0, borderRadius: '8px', background: '#1f5bb7', color: '#ffffff', font: '700 12px Space Grotesk, sans-serif', cursor: 'pointer' }}
              >
                {isEnglish ? 'Confirm & Launch Generator' : 'Confirmar & Lanzar Generador'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
