using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Declarationdetail
{
    [Key]
    public Guid declarationdetailid { get; set; }

    public Guid declarationid { get; set; }

    public Guid orderid { get; set; }

    [ForeignKey("declarationid")]
    [InverseProperty("Declarationdetails")]
    public virtual Declaration declaration { get; set; } = null!;

    [ForeignKey("orderid")]
    [InverseProperty("Declarationdetails")]
    public virtual Order order { get; set; } = null!;
}
