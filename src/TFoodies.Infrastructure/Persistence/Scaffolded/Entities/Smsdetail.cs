using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Smsdetail
{
    [Key]
    public Guid smsdetailid { get; set; }

    public Guid smsid { get; set; }

    public Guid memberid { get; set; }

    public int? section { get; set; }

    [StringLength(10)]
    public string? msgid { get; set; }

    [StringLength(1)]
    public string? statuscode { get; set; }

    public int? accountpoint { get; set; }

    [StringLength(1)]
    public string? duplicate { get; set; }

    public int issend { get; set; }

    [ForeignKey("memberid")]
    [InverseProperty("Smsdetails")]
    public virtual Member member { get; set; } = null!;

    [ForeignKey("smsid")]
    [InverseProperty("Smsdetails")]
    public virtual Sm sms { get; set; } = null!;
}
