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
/// La 015 (Recaudo y Tesorería) congela sus pares el 2026-10-27 (T0271 en su parte de congelamiento, T0337, T0404, T0424, T0438 y T0466),
/// salvo el destructivo <c>RetiroDeLaTesoreriaHeredada</c>, cuya cabecera de aprobación completa el dueño como segundo revisor.
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
        ["PostgreSql/20261007172102_RetiroDeLaCarteraHeredada"] = "f1d7081fed893a6bb6c44739b1006490c3c390c4d9b7ff3f0af345d44ea068aa",
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
        ["PostgreSql/20261018143710_RetiroDeCaptacionesHeredadas"] = "001ac29d70474f60eace1e01a90d42fae19f3440931d0d44a97bc9f2568dfb8f",
        ["PostgreSql/20261019104128_CaptacionesAportes.Designer"] = "b7676d41086904edd4c45a909aff0229c012106ce7f2d0f7d9611b0994a20bec",
        ["PostgreSql/20261019104128_CaptacionesAportes"] = "80a274ef0686b3ba6967b562479f4cff00a40e40e554e0273ebf72f9f35df82f",
        ["SqlServer/20261006075000_PlataformaParaCartera.Designer"] = "0cfe95a4e02441835428ea12d6bd53306f50a311f557229eba8491beb877e981",
        ["SqlServer/20261006075000_PlataformaParaCartera"] = "36ffc1c7fdc4a2214dde30518acb66dd69d73c59ca98c27b91d0874d665564d0",
        ["SqlServer/20261007010209_NominaRecaudaPorElContrato.Designer"] = "0610dbc147db8c029752ee182fb4327f2a93bc524decd92e05568133f03acb7a",
        ["SqlServer/20261007010209_NominaRecaudaPorElContrato"] = "0784be79c11783f19363380e43761fac5d095d89d34cbbdb612095609f73d12a",
        ["SqlServer/20261007164913_ComercioConCartera.Designer"] = "9cc92244003f377b65019573f08d4d8aa800a86fb37d4644e44fa57922214473",
        ["SqlServer/20261007164913_ComercioConCartera"] = "4ddf16bfc627ada117356389a8439f552b34822f3348d030aba7fff7bf1de4b1",
        ["SqlServer/20261007172025_RetiroDeLaCarteraHeredada.Designer"] = "99de0f7016f1cbad6ce841aafed86a59fdd87eba2843a4ed08f81e19b842263f",
        ["SqlServer/20261007172025_RetiroDeLaCarteraHeredada"] = "cb632a2da0eae666f2a141b541518bcd4654d01590316a5b5dd843f98d1fd66a",
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
        ["SqlServer/20261018143627_RetiroDeCaptacionesHeredadas"] = "e18ed77d7fd2f173d6f784fd9b14a5fb4edeb0a46582e2ae9ec624212e7ab3a8",
        ["SqlServer/20261019104056_CaptacionesAportes.Designer"] = "29398a282b24e01580bcee588e4163fe38cf5a04d68f3b1a7b79c1b9fccc96dd",
        ["SqlServer/20261019104056_CaptacionesAportes"] = "84d54dc21769e5539ca17aefab3b608f7927cf6705b1d4e803b90acc4500c847",
        // Feature 015, R0/T0 (T0271 en su parte de congelamiento y T0337, 2026-10-27): los cuatro pares iniciales, los dos que nacieron después y el de datos del retiro de los dobles.
        ["PostgreSql/20261023145248_PlataformaParaRecaudoYTesoreria.Designer"] = "fb2b94385aee847c2a44ea9b8f3d71a0736ab350641ea48eafbfa20bd2fbb0ab",
        ["PostgreSql/20261023145248_PlataformaParaRecaudoYTesoreria"] = "2b4d8c1b86c81b20732dee4224e0944e591eabc60dc29336c3355f52418283ec",
        ["PostgreSql/20261024113511_RecaudoNucleo.Designer"] = "75ae8cb1b203e29cb7e1ef0df30a8565736b71b8209d66d2aa7ec29b4dee5236",
        ["PostgreSql/20261024113511_RecaudoNucleo"] = "9bf0d02db1b08cbe7563aeb212d7c90a514e057c1a4b034a4c3bef56cb0e091c",
        ["PostgreSql/20261024113647_TesoreriaNucleo.Designer"] = "b7f781ee584e4eda15a2719992d6d69ecf562ef6e682c8182ab41029427c9760",
        ["PostgreSql/20261024113647_TesoreriaNucleo"] = "d18f971fe5ae77cafa3ffc422bb1e1bf36d7d256cde2fc9425c71f4c80304089",
        ["PostgreSql/20261024113827_IntegracionContableDeRecaudoYTesoreria.Designer"] = "69a0f2692b2659850324e8e686d3c8bf4ccd09eb9420ed6c4274e6501bea0c43",
        ["PostgreSql/20261024113827_IntegracionContableDeRecaudoYTesoreria"] = "d0d0d9d04321da1ca375030a35a764a6f8dacf87254c6185d221bfa20726ad8a",
        ["PostgreSql/20261024124206_TesoreriaConciliacionMinima.Designer"] = "fc0264b284dfb0382cd5d85a23f702b077488b653d191febd3f15648372f7256",
        ["PostgreSql/20261024124206_TesoreriaConciliacionMinima"] = "fc59693c42ad079c2ef3083543fc6ab35a4f364483da0de3c04f9dd45957ee18",
        ["PostgreSql/20261024130418_RecaudoYTesoreriaPuestaEnMarcha.Designer"] = "cd1cc8fc15b60e64084ad7bf83cb4181cb99f8611484ceec1e2aff67e4051bc2",
        ["PostgreSql/20261024130418_RecaudoYTesoreriaPuestaEnMarcha"] = "af41f3360a14b28bba8cdc3898a979ba1a1a2c1578dfb94e333e6e100037ae64",
        ["PostgreSql/20261027024928_RetiroDeLosDoblesDePrueba.Designer"] = "85835fa1a3bc3959159917b9cca86689a084b87a98504f884867ec3991ffc70b",
        ["PostgreSql/20261027024928_RetiroDeLosDoblesDePrueba"] = "d7c1933aaae1a046b4b5b1646226f9eec1b7d53799e341f2afe38eca26e0c3a4",
        ["SqlServer/20261023145208_PlataformaParaRecaudoYTesoreria.Designer"] = "40b596a6661efb6e63bbe819c5e25725c9312bee2e6d5aeeb5cedf272b306ba4",
        ["SqlServer/20261023145208_PlataformaParaRecaudoYTesoreria"] = "765b0e343220a34583e2260762e7853482c1f71409b2594167c235599db4e705",
        ["SqlServer/20261024113421_RecaudoNucleo.Designer"] = "793c40c8a8dc3df845d09a31941b102e8398c63291a7a997e01a139342ba1dee",
        ["SqlServer/20261024113421_RecaudoNucleo"] = "50b356efa9ab6e48cf1f3a521b8a83f361fa34b8cf173a99bd7821a17785d901",
        ["SqlServer/20261024113559_TesoreriaNucleo.Designer"] = "94cd822eec0cc328fb0ba345347de07bed4fca00efe920f0e45d46181b9dfc06",
        ["SqlServer/20261024113559_TesoreriaNucleo"] = "a0f2ea574c3f3f704c6ec239967fe83bd93202d5ecb2b854e9dc03fea77d4ea6",
        ["SqlServer/20261024113733_IntegracionContableDeRecaudoYTesoreria.Designer"] = "b20e9f98e499a2cae2e27e3ba1d092b16ab01922b1ed164bfc88895ed9aeddff",
        ["SqlServer/20261024113733_IntegracionContableDeRecaudoYTesoreria"] = "1bfd637c2c876ed7bb9e6d66dcb53616d6f77ff55d4535fa4256ea0b222731e2",
        ["SqlServer/20261024124117_TesoreriaConciliacionMinima.Designer"] = "41435bad808a1f528226e6a1aab20c92926f08cecdc87bc02d1c3b5eb7ebd7f5",
        ["SqlServer/20261024124117_TesoreriaConciliacionMinima"] = "b49921229eb22a8a0a0560e1644e8e6a849df4b67df15b874dc8e70ebc72a3af",
        ["SqlServer/20261024130329_RecaudoYTesoreriaPuestaEnMarcha.Designer"] = "fcb846e20f0988c6bc05d3950705dff6e67677af281322fe116193a22ef0f9ef",
        ["SqlServer/20261024130329_RecaudoYTesoreriaPuestaEnMarcha"] = "a460548c839d8fa1d0e18c01f735ffac1684857f7e2575e37c7f29351c3b5b55",
        ["SqlServer/20261027024843_RetiroDeLosDoblesDePrueba.Designer"] = "9b8a21ae4499724842d1f8c9b16762c1d855e97bbb23666b87987da0af492383",
        ["SqlServer/20261027024843_RetiroDeLosDoblesDePrueba"] = "ff75c5c0d1e0ffd2a8a96360875bf3945d6f62f00a7f35439907997358ec6aef",
        // Feature 015, R1 (T0404, 2026-10-27).
        ["PostgreSql/20261024185201_RecaudoCompleto.Designer"] = "0ab872a9af3a5693608280670263470c043e4971f41045e43485f0adfb35d964",
        ["PostgreSql/20261024185201_RecaudoCompleto"] = "3a06b830c1b232dfc1f703c752cb9939d31ec74050a0fba97ec6a0f728414a8a",
        ["PostgreSql/20261024155953_CarteraYCaptacionesConPartidaCitada.Designer"] = "b4f28a68030cbc8f613ec386f423ce90c5820e68e1b4dafb321f3f85b8f39250",
        ["PostgreSql/20261024155953_CarteraYCaptacionesConPartidaCitada"] = "40f516854b4a6061915ee08ab4b3263b3251201864c6eb7401d6841b1ad4dec9",
        ["SqlServer/20261024185110_RecaudoCompleto.Designer"] = "70a823636a5e97849d48425c18d30b89e311c6eeaa8071db1ede8387c118cdca",
        ["SqlServer/20261024185110_RecaudoCompleto"] = "2bc7504f52c789ac469f615c8e5a2b0769a26f6b8bc2a3d4a0fc0b039f0ab31f",
        ["SqlServer/20261024155853_CarteraYCaptacionesConPartidaCitada.Designer"] = "79bbf5a97d007f593b5272e2548241155e5d9e5f9309c134b238cc8a0d9e7cd2",
        ["SqlServer/20261024155853_CarteraYCaptacionesConPartidaCitada"] = "f0c8ca1fe293a42124c47837662a461f15ee6bc3154d74063f108628d02f69fd",
        // Feature 015, T1 (T0424, 2026-10-27).
        ["PostgreSql/20261024203607_TesoreriaCompleta.Designer"] = "0f38e2d9da3ac722e832791471cca0fc8b22825f8cbff7321494c952726c812f",
        ["PostgreSql/20261024203607_TesoreriaCompleta"] = "7740968b99f0fcf5b3af184a10e32ef69b39c035bf611cd032ba4fd09a5c9b6b",
        ["SqlServer/20261024203551_TesoreriaCompleta.Designer"] = "d196f6b6297fefbd246b9e62f3cd729160d4d3a48950945cc53010aa40e9b7d5",
        ["SqlServer/20261024203551_TesoreriaCompleta"] = "3aded54d97f7e97cc481217257de89c19383a298df7901fc179d9760f536cd03",
        // Feature 015, R2 (T0438, 2026-10-27).
        ["PostgreSql/20261026164905_RecaudoCanales.Designer"] = "72689eef9ce46f4eaef5eaafe6cd9d44ddc12bf34c363aea8c2fb604d040f996",
        ["PostgreSql/20261026164905_RecaudoCanales"] = "ef01b285d257ad79581893fc445b82894b9f1cecb6cfb84cb590586d00c9ca19",
        ["SqlServer/20261026164807_RecaudoCanales.Designer"] = "72e343948fe267cd341bc56df69e1d610e3a446ecab3d0999a44df9b45437409",
        ["SqlServer/20261026164807_RecaudoCanales"] = "95e33316bcaa555c374fa556054a330d7586042223d2d96015c9aadbfc08aa4f",
        // Feature 015, T2 (T0466, 2026-10-27).
        ["PostgreSql/20261026185302_TesoreriaAmpliada.Designer"] = "6bde5674074b400b0490a1fdf66153ad01e22f012580b0c9dec187329c3a4edf",
        ["PostgreSql/20261026185302_TesoreriaAmpliada"] = "79eac4760e51e004c12d64e6301cc0e373d9d38abccc33d40c2299d65038eb37",
        ["SqlServer/20261026185203_TesoreriaAmpliada.Designer"] = "7dbd8deea63c76c3e35988a093157e705c68754f3b8437ac93cb50c1ee3492dc",
        ["SqlServer/20261026185203_TesoreriaAmpliada"] = "b36ba55286cc89023c83e4bced7288e68d648e6f254d8429f6b4316197ac0e2f",
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
