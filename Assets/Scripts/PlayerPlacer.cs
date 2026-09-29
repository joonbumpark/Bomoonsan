using UnityEngine;

namespace Mountains
{
    public class PlayerPlacer : MonoBehaviour
    {
        public GameManager GameManager;
        public GameObject[] Positions;

        // ContextMenu는 매개변수 있는 메서드를 못 받아서 PlaceN() 각각을 따로 둔다.
        private bool PlacePlayer(int index)
        {
            if (index < 0 || index >= Positions.Length || Positions[index] == null)
            {
                return false;
            }

            var pos = Positions[index].transform.position;
            GameManager.PlaceOnTerrain(GameManager.Player.gameObject, pos.x, pos.z);
            return true;
        }

        [ContextMenu("Place0")]
        public void Place0() => PlacePlayer(0);

        [ContextMenu("Place1")]
        public void Place1() => PlacePlayer(1);

        [ContextMenu("Place2")]
        public void Place2() => PlacePlayer(2);

        [ContextMenu("Place3")]
        public void Place3() => PlacePlayer(3);

        [ContextMenu("Place4")]
        public void Place4() => PlacePlayer(4);
    }
}
