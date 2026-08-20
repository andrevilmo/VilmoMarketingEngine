const { extractChave } = require("../src/Vilmo.Web/wwwroot/js/nfe-scan.js");

const CHAVE = "42260868431371000161555001000000001123456788";

function assertEqual(a, b, msg) {
  if (a !== b) throw new Error(msg || `${a} !== ${b}`);
}
function assertNull(a, msg) {
  if (a != null) throw new Error(msg || `expected null, got ${a}`);
}

assertEqual(extractChave(CHAVE), CHAVE, "raw 44");
assertEqual(extractChave("4226 0868 4313 7100 0161 5550 0100 0000 0011 2345 6788"), CHAVE, "spaces");
assertEqual(
  extractChave("https://www.fazenda.pr.gov.br/nfce/qrcode?p=42260868431371000161555001000000001123456788|2|1|1|ABCDEF"),
  CHAVE,
  "nfc-e p="
);
assertEqual(
  extractChave("http://nfe.fazenda.sp.gov.br/qrcode?chNFe=42260868431371000161555001000000001123456788&nVersao=100&tpAmb=1"),
  CHAVE,
  "chNFe="
);
assertNull(extractChave("42260868431371000161555001000000001123456780"), "bad dv");
assertNull(extractChave("not a chave"), "garbage");
assertNull(extractChave(""), "empty");
// extra digits in URL must not steal the chave via slice(-44)
assertEqual(
  extractChave("http://portal 12345678901234567890123456789012345678901234.sefaz/?chNFe=" + CHAVE + "&x=999"),
  CHAVE,
  "chNFe wins over extra digits"
);
console.log("nfe-scan extractChave: ok");
