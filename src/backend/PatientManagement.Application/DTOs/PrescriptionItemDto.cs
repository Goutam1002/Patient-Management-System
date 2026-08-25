namespace PatientManagement.Application.DTOs;

public class PrescriptionItemDto
{
    public int Id { get; set; }
    public required string DrugName { get; set; }
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Instructions { get; set; }
}
