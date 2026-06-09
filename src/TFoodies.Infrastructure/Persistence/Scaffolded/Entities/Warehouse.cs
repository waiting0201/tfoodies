using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Warehouse
{
    [Key]
    public Guid warehouseid { get; set; }

    public int warehousetype { get; set; }

    [StringLength(50)]
    public string title { get; set; } = null!;

    [InverseProperty("warehouse")]
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
