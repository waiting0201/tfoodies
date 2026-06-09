using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Recipeseasoning
{
    [Key]
    public Guid recipeseasoningid { get; set; }

    public Guid recipeid { get; set; }

    [StringLength(50)]
    public string title { get; set; } = null!;

    [StringLength(50)]
    public string value { get; set; } = null!;

    public int sort { get; set; }

    [ForeignKey("recipeid")]
    [InverseProperty("Recipeseasonings")]
    public virtual Recipe recipe { get; set; } = null!;
}
