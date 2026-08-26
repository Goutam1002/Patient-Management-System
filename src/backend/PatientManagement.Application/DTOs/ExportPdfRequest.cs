namespace PatientManagement.Application.DTOs;

public class ExportPdfRequest
{
    public int PatientId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public bool Confirmed { get; set; }
}
