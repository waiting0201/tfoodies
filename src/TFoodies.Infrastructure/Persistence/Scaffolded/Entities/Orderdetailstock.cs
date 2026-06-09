using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Orderdetailstock
{
    [Key]
    public Guid orderdetailstockid { get; set; }

    public Guid orderdetailid { get; set; }

    public Guid warehousestockid { get; set; }

    public int qty { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? createdate { get; set; }

    [ForeignKey("orderdetailid")]
    [InverseProperty("Orderdetailstocks")]
    public virtual Orderdetail orderdetail { get; set; } = null!;

    [ForeignKey("warehousestockid")]
    [InverseProperty("Orderdetailstocks")]
    public virtual Warehousestock warehousestock { get; set; } = null!;
}
