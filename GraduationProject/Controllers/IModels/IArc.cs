using System.Collections.Generic;

namespace GraduationProject.Controllers.IModels;

public interface IArc: IPoint
{
    public bool ArcStatus { get; set; }
    public int ArcCount { get; set; }
    public List<double> ArcRadius { get; set; }
    public List<string> ArcCoordinates { get; set; }
}