using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 014 (T225, 2026-10-19): las migraciones de las features 013 y 014 están <b>congeladas</b>. La 013 congeló los pares de
/// K1 en T0802 (2026-10-15) y sus pares de K2 a K6b nacieron después del congelamiento; la 014 congela
/// <c>RetiroDeCaptacionesHeredadas</c> y <c>CaptacionesAportes</c> al cierre (T225), cuando dejan de ser regenerables. Desde aquí
/// todo cambio de esquema es una <b>migración nueva</b>: regenerar una de éstas (aunque conserve el sello, como hacían las fases
/// de la 014 con una migración temporal fusionada) o tocar su SQL escrito a mano cambia la huella y esta prueba lo dice.
///
/// <para>La huella es SHA-256 del texto del <c>.cs</c> y de su <c>.Designer.cs</c> sin BOM, con saltos de línea normalizados a LF
/// y sin blancos al final, para que no dependa de <c>core.autocrlf</c>. Una corrección legítima de una migración ya
/// congelada (sólo antes de promoverla fuera de local) exige actualizar la huella aquí, a propósito y con su anotación.</para>
/// </summary>
public class LasMigracionesCongeladasNoSeRegeneran
{
    private static readonly IReadOnlyDictionary<string, string> Huellas = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["PostgreSql/20261006075037_PlataformaParaCartera.Designer"] = "15d041b9149bd5abef14a5ca382a23c808422caa5b0145f926fda87d845fb975",
        ["PostgreSql/20261006075037_PlataformaParaCartera"] = "9793139d1d7137f8cbb232bcd88e6d0e9e582c68b1a236ed81842b8c8a561f98",
        ["PostgreSql/20261007010256_NominaRecaudaPorElContrato.Designer"] = "bae214595fe24a9438dbe8b02bc5a60f378de7d9fa1f371e74b2599331971618",
        ["PostgreSql/20261007010256_NominaRecaudaPorElContrato"] = "a35d2b6f9efc59ff77945fa8abacbbfb4311b847590d3a43b5e3017ef4e00b91",
        ["PostgreSql/20261007164951_ComercioConCartera.Designer"] = "05e0a1a1e17f055ce157d2b21d448065e65f7b8243b97a8874f7b9f99ab70ee8",
        ["PostgreSql/20261007164951_ComercioConCartera"] = "84382f05d344428a1dd9edb193e7b7cd40df1ecd9a276e2cb707d70df5acd74d",
        ["PostgreSql/20261007172102_RetiroDeLaCarteraHeredada.Designer"] = "2bea81ea5f24ee0035b560eb578d608d0f3c351abc00ba1eb582b776a4003f75",
        ["PostgreSql/20261007172102_RetiroDeLaCarteraHeredada"] = "ee39598e8cf39b923957c20bdb248420b66e700a802a72501d8cceb7a4d26be6",
        ["PostgreSql/20261015040557_CarteraNucleo.Designer"] = "8f760563e5794e9069d99a857a988b078e80f936e18ceacb0ded67b886aa087f",
        ["PostgreSql/20261015040557_CarteraNucleo"] = "81c6c8467ac2e2f32a455b96ec8ed71872463138f0e07d52df4bc104cea63a65",
        ["PostgreSql/20261015040710_CarteraRiesgo.Designer"] = "3f6bf680ebdf01181bcb87369b18ebe04c9512f47f10bdedc62dde9de3324f92",
        ["PostgreSql/20261015040710_CarteraRiesgo"] = "b5bb4ffc36c0119ac7b0e145476b684c6cbece62d91774860d8170731f01368b",
        ["PostgreSql/20261015040801_IntegracionContableDeCartera.Designer"] = "a25fcd87f2cf1f7b2319087abfcd0f8f9e35a67574f1b07faece5532187212aa",
        ["PostgreSql/20261015040801_IntegracionContableDeCartera"] = "f4e3412680ede9bf4b3c34e2b6a7523612855a6a2e12c5bc6c3275ba960a470c",
        ["PostgreSql/20261015140615_CarteraLibranza.Designer"] = "e35998b2fd052c80cbc1acc22e00e6b1223f16e51611915d7cd5d599a6c12286",
        ["PostgreSql/20261015140615_CarteraLibranza"] = "70be0d7cabb633094a6293354950ec564f477f0e61dd76b5bef74af46ab400d1",
        ["PostgreSql/20261015195513_CarteraNovedades.Designer"] = "45fe9f11ff7e5bf16ab6c5821df596e8c014faac4c11700b4bd8eba792788832",
        ["PostgreSql/20261015195513_CarteraNovedades"] = "2ce586463729f5863af4578eb43a498ed45c873bbfc367e9a3ffb860331defbf",
        ["PostgreSql/20261016010144_CarteraCobranza.Designer"] = "8c1f924957635c6523bf38e8b4bda06d8d02008221d0de52c2beecea5c16fbef",
        ["PostgreSql/20261016010144_CarteraCobranza"] = "0972aa7589fc494af815a16d3e5f1d86d0c3d30ddccac74d0951f0b759824add",
        ["PostgreSql/20261016183603_CarteraCobranzaSalida.Designer"] = "99d0a757628c11cc7d62c61411e9d0e2f573829eb0ea1a0722fb60653b16613b",
        ["PostgreSql/20261016183603_CarteraCobranzaSalida"] = "e58c8114d98c9f944b9fbbd4fa897db1a097098e1c0276bf15e99dad9015f04d",
        ["PostgreSql/20261017012810_CarteraInformes.Designer"] = "3a84eaa3697770a26dbfb0dfd938f0942fd68c3f96c4582c426ba70cf022f964",
        ["PostgreSql/20261017012810_CarteraInformes"] = "982cbc1d5b37966315e1650898eb64fcf42836a473e8030e580dfb9beb0d067b",
        ["PostgreSql/20261017031706_CarteraComprobanteDeCargue.Designer"] = "817c04be6470570aef7d1b0c7d32bf16161e95375d0714ac5a93acb3832fdad6",
        ["PostgreSql/20261017031706_CarteraComprobanteDeCargue"] = "348a2517bcd46129f29da576f7e53b75babf520b8d01b14ca9062881fa4212f6",
        ["PostgreSql/20261017042947_CarteraPerdidaEsperada.Designer"] = "d95b043de12232327a1f321b53ad769206efea7921cea4ead969638253634fe7",
        ["PostgreSql/20261017042947_CarteraPerdidaEsperada"] = "3717e8ea7cd4e84e21310dd9399fd22a895be8661ca14c19827ec7cbac3586f8",
        ["PostgreSql/20261017062235_CarteraVivienda.Designer"] = "0519e2f8a88fa6f4338dbd004d2f03a220587d5f9bd76526e227a1519f3e9ea4",
        ["PostgreSql/20261017062235_CarteraVivienda"] = "7a2e99f591365210234f4655742257648b12c026824ff39771ab1da8e00cc66b",
        ["PostgreSql/20261018143710_RetiroDeCaptacionesHeredadas.Designer"] = "4399221379f05bbe094739499f27d211f4c5a35ae7e7d73f00545077d70827be",
        ["PostgreSql/20261018143710_RetiroDeCaptacionesHeredadas"] = "0fa94fc7ac01e0e43b0fe6caf72acaf2cb97bfe59e0271ee40c52eb9fd978020",
        ["PostgreSql/20261019104128_CaptacionesAportes.Designer"] = "b7676d41086904edd4c45a909aff0229c012106ce7f2d0f7d9611b0994a20bec",
        ["PostgreSql/20261019104128_CaptacionesAportes"] = "80a274ef0686b3ba6967b562479f4cff00a40e40e554e0273ebf72f9f35df82f",
        ["SqlServer/20261006075000_PlataformaParaCartera.Designer"] = "0cfe95a4e02441835428ea12d6bd53306f50a311f557229eba8491beb877e981",
        ["SqlServer/20261006075000_PlataformaParaCartera"] = "36ffc1c7fdc4a2214dde30518acb66dd69d73c59ca98c27b91d0874d665564d0",
        ["SqlServer/20261007010209_NominaRecaudaPorElContrato.Designer"] = "0610dbc147db8c029752ee182fb4327f2a93bc524decd92e05568133f03acb7a",
        ["SqlServer/20261007010209_NominaRecaudaPorElContrato"] = "0784be79c11783f19363380e43761fac5d095d89d34cbbdb612095609f73d12a",
        ["SqlServer/20261007164913_ComercioConCartera.Designer"] = "9cc92244003f377b65019573f08d4d8aa800a86fb37d4644e44fa57922214473",
        ["SqlServer/20261007164913_ComercioConCartera"] = "4ddf16bfc627ada117356389a8439f552b34822f3348d030aba7fff7bf1de4b1",
        ["SqlServer/20261007172025_RetiroDeLaCarteraHeredada.Designer"] = "99de0f7016f1cbad6ce841aafed86a59fdd87eba2843a4ed08f81e19b842263f",
        ["SqlServer/20261007172025_RetiroDeLaCarteraHeredada"] = "3f29a397c5c423ec81c3c33d3963326f50edaa12a54b7ea1956c590bc74722cc",
        ["SqlServer/20261015040531_CarteraNucleo.Designer"] = "62ef14f907e6d39de4fe82646fb52045779b22b00bbd2ba675d97eaf5127a32e",
        ["SqlServer/20261015040531_CarteraNucleo"] = "e296ffab3cbdbbebec93f448590fb93188cb08c49a2e7ebd3878479915be5313",
        ["SqlServer/20261015040644_CarteraRiesgo.Designer"] = "14abeb9781546c0d82f9cef2487a40a92e95e7f84a0c25574a98717c90d475ba",
        ["SqlServer/20261015040644_CarteraRiesgo"] = "847ef483872eeecbed2b9b562c1a5c3dfabaa8a688ad5a9b70f9133ebb47d62e",
        ["SqlServer/20261015040735_IntegracionContableDeCartera.Designer"] = "00bb5c6319b39a9409bb0b4086d99f9b0440693791f5a2aade0b5c5a16dfe206",
        ["SqlServer/20261015040735_IntegracionContableDeCartera"] = "ca0ca946c9a191833b33fd14ce230fc030e8f26336f8b74570ae76f350561cd8",
        ["SqlServer/20261015140547_CarteraLibranza.Designer"] = "1f44fe71339cdfa36201b72212c3ef88e1b2d76e5a2199a0bd79518c47692c9c",
        ["SqlServer/20261015140547_CarteraLibranza"] = "53cb8f369f93ac685650df2462176cadaa1e4f552386f76164d16ecb4e10f4a0",
        ["SqlServer/20261015195430_CarteraNovedades.Designer"] = "219b977538234fb363d079312ace2358d21bc5c9021bb89e0042d1db7ce39cb7",
        ["SqlServer/20261015195430_CarteraNovedades"] = "38f951044c2c9916b8c3d4609c94d7c48c61e99143e946ff16d5030ddc721b2b",
        ["SqlServer/20261016010113_CarteraCobranza.Designer"] = "dbd3c03b887516cddd3f688f149e1d99ed514552d8785e2333fc4dca00308b28",
        ["SqlServer/20261016010113_CarteraCobranza"] = "b640687cdd1b42ed9ed899c575f1e0d0faba33c2db84229427730a917ec88637",
        ["SqlServer/20261016183514_CarteraCobranzaSalida.Designer"] = "275a80bdaa41f1aeff7850f69470904805eaff0cef6126c39bcd3f3c932cabfc",
        ["SqlServer/20261016183514_CarteraCobranzaSalida"] = "6804fe7939f22c9891ee50e2de17f4a89f21f0c056b073ca5c68d1e559ad6655",
        ["SqlServer/20261017012740_CarteraInformes.Designer"] = "8ee802b8be7c1120c4b5f3287e5664766a4cfb7e8dc96a5d2f2ec032e95bd71d",
        ["SqlServer/20261017012740_CarteraInformes"] = "b7ca9b000134a6418873a7fc1b47ac0570e80f7a386b5931c18c37f0abdf08d0",
        ["SqlServer/20261017031635_CarteraComprobanteDeCargue.Designer"] = "020e3d3502fec8143a3491b9d2b1a30fe362064db7b947405fb79476b360fef4",
        ["SqlServer/20261017031635_CarteraComprobanteDeCargue"] = "5db0a8cd818ecdcc45a66d99a468299663683ecc72946613ef0f6bc9fd2bdf70",
        ["SqlServer/20261017042918_CarteraPerdidaEsperada.Designer"] = "9062a071e621dcf999143a06eda42ac98372e7715216cf06db71a491ae51a703",
        ["SqlServer/20261017042918_CarteraPerdidaEsperada"] = "e97563c6f8a2516d0d27157ae94beeb41ccdd90febc95f59c3a049f8e38af1b6",
        ["SqlServer/20261017062205_CarteraVivienda.Designer"] = "4cf739526bc8c3a3e385585fb00ddc19255227b701306564ce4ed339843a20cc",
        ["SqlServer/20261017062205_CarteraVivienda"] = "405a8c5d6277717e836dcb7ea7757911f3e9330c5fbfb85f7dea3a5eb972b38e",
        ["SqlServer/20261018143627_RetiroDeCaptacionesHeredadas.Designer"] = "70ed57013c34444af64b1a365396fe0ff05037d6c9cd6de0209893df48cfe9d5",
        ["SqlServer/20261018143627_RetiroDeCaptacionesHeredadas"] = "ea88fbf1420fcb1433b64558417d2e23ae88ea56acaebe9abb4be1a259b31f71",
        ["SqlServer/20261019104056_CaptacionesAportes.Designer"] = "29398a282b24e01580bcee588e4163fe38cf5a04d68f3b1a7b79c1b9fccc96dd",
        ["SqlServer/20261019104056_CaptacionesAportes"] = "84d54dc21769e5539ca17aefab3b608f7927cf6705b1d4e803b90acc4500c847",
    };

    public static TheoryData<string> Congeladas()
    {
        var datos = new TheoryData<string>();
        foreach (var clave in Huellas.Keys.Order(StringComparer.Ordinal)) datos.Add(clave);
        return datos;
    }

    [Theory]
    [MemberData(nameof(Congeladas))]
    public void La_migracion_congelada_no_cambio(string clave)
    {
        var (motor, nombre) = (clave[..clave.IndexOf('/')], clave[(clave.IndexOf('/') + 1)..]);
        var archivo = Path.Combine(RepoPath.FindRepoRoot(), "src", "Infrastructure",
            $"IngenIA365ERP.Persistence.Migrations.{motor}", "Application", nombre + ".cs");

        Assert.True(File.Exists(archivo), $"La migración congelada {clave} desapareció: una congelada no se borra ni se regenera con otro sello.");

        var texto = File.ReadAllText(archivo, Encoding.UTF8).Replace("\r\n", "\n").TrimEnd();
        var huella = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));

        Assert.True(Huellas[clave] == huella,
            $"La migración congelada {clave} cambió. Desde T0802 (013) y T225 (014) todo cambio de esquema es una migración nueva, nunca una regeneración.");
    }

    [Fact]
    public void Ambos_motores_congelan_las_mismas_migraciones()
    {
        static IEnumerable<string> Logicas(string motor) => Huellas.Keys
            .Where(k => k.StartsWith(motor + "/", StringComparison.Ordinal))
            .Select(k => k[(k.IndexOf('_') + 1)..])
            .Order(StringComparer.Ordinal);

        Assert.Equal(Logicas("PostgreSql"), Logicas("SqlServer"));
        Assert.Contains("CaptacionesAportes", Logicas("PostgreSql"));
        Assert.Contains("RetiroDeCaptacionesHeredadas", Logicas("PostgreSql"));
    }
}
