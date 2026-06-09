using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Question
{
    [Key]
    public Guid questionid { get; set; }

    public Guid questiontypeid { get; set; }

    [StringLength(150)]
    public string title { get; set; } = null!;

    [Column(TypeName = "ntext")]
    public string answer { get; set; } = null!;

    public int sort { get; set; }

    [InverseProperty("question")]
    public virtual ICollection<Questionmedia> Questionmedia { get; set; } = new List<Questionmedia>();

    [ForeignKey("questiontypeid")]
    [InverseProperty("Questions")]
    public virtual Questiontype questiontype { get; set; } = null!;
}
