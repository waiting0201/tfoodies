using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Questiontype
{
    [Key]
    public Guid questiontypeid { get; set; }

    [StringLength(50)]
    public string title { get; set; } = null!;

    public int sort { get; set; }

    [InverseProperty("questiontype")]
    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
}
