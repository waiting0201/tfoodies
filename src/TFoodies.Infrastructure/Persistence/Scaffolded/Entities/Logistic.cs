using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Logistic
{
    [Key]
    public Guid logisticid { get; set; }

    [StringLength(8)]
    public string logisticcode { get; set; } = null!;

    [StringLength(25)]
    public string title { get; set; } = null!;

    [StringLength(150)]
    public string? address { get; set; }

    [StringLength(20)]
    public string? phone { get; set; }

    public bool isenable { get; set; }

    [InverseProperty("logistic")]
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
