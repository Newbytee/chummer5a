/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Chummer
{
    public partial class SelectDiceHits : Form
    {
        #region Control Events

        public SelectDiceHits()
        {
            InitializeComponent();
            this.UpdateLightDarkMode();
            this.TranslateWinForm();
        }

        private async void SelectDiceHits_Load(object sender, EventArgs e)
        {
            CursorWait objCursorWait = await CursorWait.NewAsync(this).ConfigureAwait(false);
            try
            {
                string strSpace = await LanguageManager.GetStringAsync("String_Space").ConfigureAwait(false);
                string strText = await LanguageManager.GetStringAsync("String_DiceHits_HitsOn").ConfigureAwait(false) + strSpace
                    + Dice.ToString(GlobalSettings.CultureInfo)
                    + await LanguageManager.GetStringAsync("String_D6").ConfigureAwait(false)
                    + await LanguageManager.GetStringAsync("String_Colon").ConfigureAwait(false) + strSpace;
                await lblDice.DoThreadSafeAsync(x => x.Text = strText).ConfigureAwait(false);
                await nudDiceResult.DoThreadSafeAsync(x =>
                {
                    x.Maximum = Dice * 6;
                    x.Minimum = 6;
                }).ConfigureAwait(false);
                await DoRoll().ConfigureAwait(false);
            }
            finally
            {
                await objCursorWait.DisposeAsync().ConfigureAwait(false);
            }
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private async void cmdRoll_Click(object sender, EventArgs e)
        {
            CursorWait objCursorWait = await CursorWait.NewAsync(this).ConfigureAwait(false);
            try
            {
                await DoRoll().ConfigureAwait(false);
            }
            finally
            {
                await objCursorWait.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async ValueTask DoRoll(CancellationToken token = default)
        {
            int intResult = 0;
            for (int i = 0; i < Dice; ++i)
            {
                intResult += await GlobalSettings.RandomGenerator.NextD6ModuloBiasRemovedAsync(token: token).ConfigureAwait(false);
            }

            await nudDiceResult.DoThreadSafeAsync(x => x.ValueAsInt = intResult, token: token).ConfigureAwait(false);
        }

        #endregion Control Events

        #region Properties

        private int _intDice;

        /// <summary>
        /// Number of dice that are rolled for the lifestyle.
        /// </summary>
        public int Dice
        {
            get => _intDice;
            set
            {
                if (Interlocked.Exchange(ref _intDice, value) == value)
                    return;
                nudDiceResult.SuspendLayout();
                try
                {
                    nudDiceResult.MinimumAsInt
                        = int.MinValue; // Temporarily set this to avoid crashing if we shift from something with more than 6 dice to something with less.
                    nudDiceResult.MaximumAsInt = value * 6;
                    nudDiceResult.MinimumAsInt = value;
                }
                finally
                {
                    nudDiceResult.ResumeLayout();
                }
            }
        }

        /// <summary>
        /// Description text.
        /// </summary>
        public string Description
        {
            set => lblDescription.Text = value;
        }

        /// <summary>
        /// Dice roll result.
        /// </summary>
        public int Result => nudDiceResult.ValueAsInt;

        #endregion Properties
    }
}
