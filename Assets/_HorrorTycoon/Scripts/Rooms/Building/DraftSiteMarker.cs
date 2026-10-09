using UnityEngine;

namespace HorrorTycoon.Rooms.Building
{
    /// <summary>
    /// Casa por escolha: marca a folha fechada de uma PORTA PARA O VAZIO (HouseDoorSite). O clique do RunPresenter
    /// acha a porta por este componente (ou pela marcação de fita no chão, via HouseBuilder.SiteAt).
    /// </summary>
    public class DraftSiteMarker : MonoBehaviour
    {
        [SerializeField] private int siteIndex = -1;

        public int SiteIndex => siteIndex;

        public void Setup(int site) => siteIndex = site;
    }
}
