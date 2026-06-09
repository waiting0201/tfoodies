using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TFoodies.Infrastructure.Persistence.Scaffolded;

public partial class Zipcode
{
    [Key]
    public int zipcodeid { get; set; }

    public int countryid { get; set; }

    [StringLength(10)]
    public string city { get; set; } = null!;

    [StringLength(10)]
    public string area { get; set; } = null!;

    [StringLength(10)]
    public string zipcode { get; set; } = null!;

    public int isdisplay { get; set; }

    [InverseProperty("zipcode")]
    public virtual ICollection<Member> Members { get; set; } = new List<Member>();

    [InverseProperty("reciverzipcode")]
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
