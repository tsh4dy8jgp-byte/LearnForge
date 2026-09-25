using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Learning;

public sealed record EnrollmentRequest([property: EnumDataType(typeof(EnrollmentStatus))] EnrollmentStatus Status);
