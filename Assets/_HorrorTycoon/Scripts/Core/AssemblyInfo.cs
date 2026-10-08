using System.Runtime.CompilerServices;

// Os testes EditMode (HorrorTycoon.Tests) podem montar situações exatas (posição do vilão, pavor, tensão)
// mexendo nos campos 'internal' do estado da run. Só os testes; o resto do jogo usa a API pública.
[assembly: InternalsVisibleTo("HorrorTycoon.Tests")]
