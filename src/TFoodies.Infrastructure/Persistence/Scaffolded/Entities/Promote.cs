using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Promote
{
    [Key]
    public Guid promoteid { get; set; }

    [StringLength(50)]
    public string? title { get; set; }

    public DateOnly startdate { get; set; }

    public DateOnly expiredate { get; set; }

    /// <summary>
    /// 0 =&gt; 折扣%; 1 =&gt; 金額; 2 =&gt; 扣金額
    /// </summary>
    public int type { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal discount { get; set; }

    public int sort { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }

    [InverseProperty("promote")]
    public virtual ICollection<Promoteproduct> Promoteproducts { get; set; } = new List<Promoteproduct>();
}
