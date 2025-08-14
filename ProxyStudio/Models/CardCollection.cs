/*
ProxyStudio - A cross-platform proxy management application.
Copyright (C) 2025 James Ponter

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;


namespace ProxyStudio.Models
{
    public partial class CardCollection : ObservableCollection<Card>
    {
        
        


        public void RemoveAllCards()
        {
            this.Clear();
        }


        public  void AddCard(Card card)
        {
            this.Add(card);
        }

        public void RemoveCard(Card card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card), "Card cannot be null.");
            }
            this.Remove(card);
        }

        public Card? FindCardbyId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("ID cannot be null or empty.", nameof(id));
            }
            return this.FirstOrDefault(card => card.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        public Card GetCard(int index)
        {
            if (index < 0 || index >= this.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");
            }
            return this[index];
        }

        public void AddRange(IEnumerable<Card> newCards)
        {
            foreach (var card in newCards)
                Items.Add(card);

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
        }


        public int NumberOfCards
        {
            get { return this.Count; }
        }

        // public IEnumerator<Card> GetEnumerator() => Cards.GetEnumerator();
        // IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


       

    }
}