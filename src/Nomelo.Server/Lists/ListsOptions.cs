using System.ComponentModel.DataAnnotations;

namespace Nomelo.Server.Lists;

public class ListsOptions
{
    public const string SectionName = "Lists";

    [Required]
    [MinLength(1)]
    public string Directory { get; set; } = "";
}
