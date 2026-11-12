namespace HumanOS.Models.Family;

/// <summary>
/// Represents educational history and current schooling situation for a family member
/// (e.g. Primaria, Secundaria, Preparatoria/High School, Universidad).
/// </summary>
public sealed class FamilyMemberEducation
{
    public Guid EducationId { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid FamilyId { get; set; }

    public Guid MemberId { get; set; }

    /// <summary>Nivel educativo: Primaria, Secundaria, Preparatoria, Universidad, Posgrado, etc.</summary>
    public string EducationLevel { get; set; } = null!;

    /// <summary>Nombre de la escuela, colegio o institución educativa.</summary>
    public string SchoolName { get; set; } = null!;

    /// <summary>Grado actual o título cursado (ej. "5to de Primaria", "2do año").</summary>
    public string? GradeOrDegree { get; set; }

    /// <summary>Área de estudio, especialidad o enfoque académico.</summary>
    public string? FieldOfStudy { get; set; }

    /// <summary>Año de inicio.</summary>
    public int? StartYear { get; set; }

    /// <summary>Año de finalización (o previsto).</summary>
    public int? EndYear { get; set; }

    /// <summary>Indica si el miembro se encuentra cursando este nivel/escuela actualmente.</summary>
    public bool IsCurrent { get; set; } = true;

    /// <summary>Ciudad o ubicación de la institución.</summary>
    public string? City { get; set; }

    /// <summary>Estado, provincia o región de la institución.</summary>
    public string? State { get; set; }

    /// <summary>País de la institución.</summary>
    public string? Country { get; set; }

    /// <summary>Promedio de calificaciones / GPA.</summary>
    public string? Gpa { get; set; }

    /// <summary>Título obtenido o degree profesional (ej. Bachelor of Science in CS, Licenciatura en Derecho).</summary>
    public string? Degree { get; set; }

    /// <summary>Estado académico: Cursando actualmente ("Current") o Terminado ("Completed").</summary>
    public string? Status { get; set; }

    /// <summary>Notas, logros destacados o detalles adicionales.</summary>
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public FamilyMaster? Family { get; set; }

    public FamilyMember? Member { get; set; }
}
