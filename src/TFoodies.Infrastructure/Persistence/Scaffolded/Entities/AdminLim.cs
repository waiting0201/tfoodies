using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class AdminLim
{
    [Key]
    public Guid AdminLimID { get; set; }

    public int AdminID { get; set; }

    public int LimID { get; set; }

    public bool IsAdd { get; set; }

    public bool IsUpdate { get; set; }

    public bool IsDelete { get; set; }

    [ForeignKey("AdminID")]
    [InverseProperty("AdminLims")]
    public virtual Admin Admin { get; set; } = null!;

    [ForeignKey("LimID")]
    [InverseProperty("AdminLims")]
    public virtual Lim Lim { get; set; } = null!;
}
