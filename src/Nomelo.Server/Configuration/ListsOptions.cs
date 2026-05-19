using System.ComponentModel.DataAnnotations;

namespace Nomelo.Server.Configuration;

public class ListsOptions
{
    public const string SectionName = "Lists";

    [Required]
    [MinLength(1)]
    public string Directory { get; set; } = "";
}
