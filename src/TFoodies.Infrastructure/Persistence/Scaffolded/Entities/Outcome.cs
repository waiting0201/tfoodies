using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Outcome
{
    [Key]
    public Guid outcomeid { get; set; }

    public Guid expenditureid { get; set; }

    [StringLength(15)]
    public string outcomecode { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime outcomedate { get; set; }

    public int amount { get; set; }

    [Column(TypeName = "ntext")]
    public string? note { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }

    [ForeignKey("expenditureid")]
    [InverseProperty("Outcomes")]
    public virtual Expenditure expenditure { get; set; } = null!;
}
