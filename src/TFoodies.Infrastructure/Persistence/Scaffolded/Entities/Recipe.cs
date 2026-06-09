using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Recipe
{
    [Key]
    public Guid recipeid { get; set; }

    [StringLength(80)]
    public string title { get; set; } = null!;

    public int duration { get; set; }

    public int portion { get; set; }

    [StringLength(200)]
    public string intro { get; set; } = null!;

    [StringLength(50)]
    public string rphoto { get; set; } = null!;

    [StringLength(50)]
    public string photo { get; set; } = null!;

    [StringLength(250)]
    public string? youtube { get; set; }

    [StringLength(20)]
    public string? v { get; set; }

    [StringLength(150)]
    public string? keyword { get; set; }

    [StringLength(200)]
    public string? description { get; set; }

    public int sort { get; set; }

    [StringLength(150)]
    public string? shortener { get; set; }

    [InverseProperty("recipe")]
    public virtual ICollection<Recipeingredient> Recipeingredients { get; set; } = new List<Recipeingredient>();

    [InverseProperty("recipe")]
    public virtual ICollection<Recipeseasoning> Recipeseasonings { get; set; } = new List<Recipeseasoning>();

    [InverseProperty("recipe")]
    public virtual ICollection<Recipestep> Recipesteps { get; set; } = new List<Recipestep>();

    [ForeignKey("recipeid")]
    [InverseProperty("recipes")]
    public virtual ICollection<Issue> issues { get; set; } = new List<Issue>();

    [ForeignKey("recipeid")]
    [InverseProperty("recipes")]
    public virtual ICollection<Product> products { get; set; } = new List<Product>();
}
