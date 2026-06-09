using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Declaration
{
    [Key]
    public Guid declarationid { get; set; }

    /// <summary>
    /// 1:月; 2:日
    /// </summary>
    public int declarationtype { get; set; }

    public DateOnly declarationdate { get; set; }

    /// <summary>
    /// 1:下游業者; 2:消費者; 3: 自用
    /// </summary>
    public int soldtarget { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }

    [InverseProperty("declaration")]
    public virtual ICollection<Declarationdetail> Declarationdetails { get; set; } = new List<Declarationdetail>();
}
