namespace PatientManagement.Api.Models;

public class Visit
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    // Required in every case, scheduled or walk-in -- a visit always
    // originates from an appointment record.
    public int AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    // Sequential per patient (1, 2, 3, ...), not a global visit number.
    public int VisitNumber { get; set; }

    // Vitals -- mandatory at data-entry time, non-nullable columns.
    public decimal Temperature { get; set; } // Celsius, no unit toggle
    public short BpSystolic { get; set; }
    public short BpDiastolic { get; set; }
    public int Pulse { get; set; }
    public decimal Weight { get; set; } // kilograms, decimal(6,3)

    public string? Complaints { get; set; }
    public string? Diagnosis { get; set; }
}
