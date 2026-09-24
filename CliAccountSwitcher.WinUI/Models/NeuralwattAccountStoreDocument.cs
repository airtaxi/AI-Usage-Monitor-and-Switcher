using System.Collections.Generic;

namespace CliAccountSwitcher.WinUI.Models;

public sealed class NeuralwattAccountStoreDocument
{
    public int Version { get; set; } = 1;

    public List<NeuralwattAccount> Accounts { get; set; } = [];

    public string ActiveAccountIdentifier { get; set; } = "";
}
