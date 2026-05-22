namespace PrediCop.Infrastructure.Services;

/// <summary>Utilitaires de géofencing (algorithmique pure, sans dépendances).</summary>
public class GeofencingService
{
    /// <summary>
    /// Ray-casting algorithm: détermine si un point est dans un polygone.
    /// </summary>
    /// <param name="lat">Latitude du point à tester.</param>
    /// <param name="lon">Longitude du point à tester.</param>
    /// <param name="vertices">Liste ordonnée des sommets du polygone.</param>
    /// <returns>True si le point est à l'intérieur du polygone.</returns>
    public static bool IsInsidePolygon(double lat, double lon, IList<(double lat, double lon)> vertices)
    {
        int n = vertices.Count;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var vi = vertices[i];
            var vj = vertices[j];
            if (vi.lon > lon != vj.lon > lon &&
                lat < (vj.lat - vi.lat) * (lon - vi.lon) / (vj.lon - vi.lon) + vi.lat)
                inside = !inside;
        }
        return inside;
    }
}
