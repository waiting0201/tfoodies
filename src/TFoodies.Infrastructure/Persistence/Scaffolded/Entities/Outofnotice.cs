using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Outofnotice
{
    [Key]
    public Guid outofnoticeid { get; set; }

    public Guid productid { get; set; }

    public Guid? memberid { get; set; }

    [StringLength(20)]
    public string name { get; set; } = null!;

    [StringLength(150)]
    public string email { get; set; } = null!;

    [StringLength(15)]
    public string? mobile { get; set; }

    public DateOnly createdate { get; set; }

    public bool isnotice { get; set; }

    [ForeignKey("memberid")]
    [InverseProperty("Outofnotices")]
    public virtual Member? member { get; set; }

    [ForeignKey("productid")]
    [InverseProperty("Outofnotices")]
    public virtual Product product { get; set; } = null!;
}
