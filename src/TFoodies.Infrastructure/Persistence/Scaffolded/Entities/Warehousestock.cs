using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Warehousestock
{
    [Key]
    public Guid warehousestockid { get; set; }

    public Guid warehouseid { get; set; }

    public Guid stockid { get; set; }

    public int quantity { get; set; }

    public int quantity_left { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime transdate { get; set; }

    [StringLength(250)]
    public string? memo { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }

    [InverseProperty("warehousestock")]
    public virtual ICollection<Orderdetailstock> Orderdetailstocks { get; set; } = new List<Orderdetailstock>();

    [ForeignKey("stockid")]
    [InverseProperty("Warehousestocks")]
    public virtual Stock stock { get; set; } = null!;
}
