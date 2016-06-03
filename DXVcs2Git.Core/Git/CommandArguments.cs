using System.Globalization;
using System.Text;

namespace DXVcs2Git.Core.Git {
    public class CommandArguments {
        readonly StringBuilder command;

        public CommandArguments() {
            this.command = new StringBuilder();
        }

        public void AddArg(string argument, params object[] args) {
            this.command.AppendFormat(CultureInfo.InvariantCulture, argument, args);
            this.command.Append(" ");
        }

        public override string ToString() {
            return this.command.ToString();
        }
    }
}