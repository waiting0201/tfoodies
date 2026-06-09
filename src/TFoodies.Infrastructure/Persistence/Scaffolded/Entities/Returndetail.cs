using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Returndetail
{
    [Key]
    public Guid returndetailid { get; set; }

    public Guid returnid { get; set; }

    public Guid orderdetailid { get; set; }

    public Guid accountingid { get; set; }

    public int qty { get; set; }

    public int price { get; set; }

    [ForeignKey("returnid")]
    [InverseProperty("Returndetails")]
    public virtual Return _return { get; set; } = null!;

    [ForeignKey("accountingid")]
    [InverseProperty("Returndetails")]
    public virtual Accounting accounting { get; set; } = null!;

    [ForeignKey("orderdetailid")]
    [InverseProperty("Returndetails")]
    public virtual Orderdetail orderdetail { get; set; } = null!;
}
