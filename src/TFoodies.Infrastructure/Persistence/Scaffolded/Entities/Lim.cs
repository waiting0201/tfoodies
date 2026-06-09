using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Lim
{
    [Key]
    public int LimID { get; set; }

    [StringLength(50)]
    public string? Key { get; set; }

    [StringLength(50)]
    public string? Value { get; set; }

    [StringLength(50)]
    public string? Icon { get; set; }

    public int Sort { get; set; }

    public int? ParentID { get; set; }

    [InverseProperty("Lim")]
    public virtual ICollection<AdminLim> AdminLims { get; set; } = new List<AdminLim>();

    [InverseProperty("Parent")]
    public virtual ICollection<Lim> InverseParent { get; set; } = new List<Lim>();

    [ForeignKey("ParentID")]
    [InverseProperty("InverseParent")]
    public virtual Lim? Parent { get; set; }
}
