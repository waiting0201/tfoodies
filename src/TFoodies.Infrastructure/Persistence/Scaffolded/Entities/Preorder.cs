using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Preorder
{
    [Key]
    public Guid preorderid { get; set; }

    [StringLength(15)]
    public string preordercode { get; set; } = null!;

    public DateOnly preorderdate { get; set; }

    [StringLength(20)]
    public string recivername { get; set; } = null!;

    [StringLength(15)]
    public string recivermobile { get; set; } = null!;

    [StringLength(50)]
    public string reciveremail { get; set; } = null!;

    public int reciverzipcodeid { get; set; }

    [StringLength(150)]
    public string reciveraddress { get; set; } = null!;

    public int recivertime { get; set; }

    public int total { get; set; }

    public int paytype { get; set; }

    public int invoicetype { get; set; }

    [StringLength(50)]
    public string? companytitle { get; set; }

    [StringLength(8)]
    public string? companynumber { get; set; }

    [StringLength(16)]
    public string? codeatm { get; set; }

    public DateOnly? expirepaydate { get; set; }

    public int paystatus { get; set; }

    public DateOnly? paydate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime createdate { get; set; }

    [InverseProperty("preorder")]
    public virtual ICollection<Preorderdetail> Preorderdetails { get; set; } = new List<Preorderdetail>();
}
