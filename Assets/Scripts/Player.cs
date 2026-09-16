using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Mountains
{
    public class Player : MonoBehaviour
    {
        public GameManager GameManager;
        public GameObject Slot;
        public GameObject[] Coins;

        public int CoinAmount => GameManager.CoinAmount;

        void Awake()
        {
            if (GameManager == null)
            {
                GameManager = FindObjectOfType<GameManager>();
            }
        }

        public void SetActiveSlot(bool active)
        {
            Slot?.SetActive(active);
        }

        public void AddCoin()
        {
            GameManager.IncCoin();

            Coins[0]?.SetActive(CoinAmount >= 1);
            Coins[1]?.SetActive(CoinAmount >= 4);
            Coins[2]?.SetActive(CoinAmount >= 8);
        }
    }
}
