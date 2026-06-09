using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Recipestep
{
    [Key]
    public Guid recipestepid { get; set; }

    public Guid recipeid { get; set; }

    [StringLength(100)]
    public string title { get; set; } = null!;

    [Column(TypeName = "ntext")]
    public string value { get; set; } = null!;

    public int sort { get; set; }

    [ForeignKey("recipeid")]
    [InverseProperty("Recipesteps")]
    public virtual Recipe recipe { get; set; } = null!;
}
