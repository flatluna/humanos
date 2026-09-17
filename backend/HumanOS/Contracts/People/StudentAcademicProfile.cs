namespace HumanOS.Contracts.People;

public sealed class StudentAcademicProfile
{
    public string? Institution { get; set; }
    public string? Degree { get; set; }
    public int? GraduationYear { get; set; }
    public string? AcademicStage { get; set; }
    public decimal? CumulativeAverage { get; set; }
    public string? AverageScale { get; set; }
    public List<string> Distinctions { get; set; } = [];
    public List<AcademicSemester> Semesters { get; set; } = [];
}

public sealed class AcademicSemester
{
    public string? Name { get; set; }
    public decimal? Average { get; set; }
    public List<AcademicSubject> Subjects { get; set; } = [];
}

public sealed class AcademicSubject
{
    public string? Name { get; set; }
    public decimal? Grade { get; set; }
}
