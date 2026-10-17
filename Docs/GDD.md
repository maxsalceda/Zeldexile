# Zeldexile — Documento de Design (v0.2)

> "Zeldexile" é o nome de trabalho. Universo, nomes, arte, música e UI são próprios.
> **Padrão de design: tudo neste jogo segue a Bíblia de Design no formato de *Breath of the Wild***
> (documento vivo: [claude.ai/code/artifact/a37f7bd0-7e67-4e64-8dee-852e05bb0b9a](https://claude.ai/code/artifact/a37f7bd0-7e67-4e64-8dee-852e05bb0b9a);
> cópia em [Docs/Biblia-de-Design.md](Biblia-de-Design.md) quando exportada).
> Copiamos o que o jogo de referência **faz**, nunca a **cara** dele (nada de nomes, arte, ícones, UI ou mapas dele).

## 1. Gancho central

**O mundo tem uma física e uma química únicas, e tudo que o jogador faz passa por elas.**

Toda ferramenta é dada no início e nunca é substituída: a profundidade vem de combinar as ferramentas com o
mundo (gameplay multiplicativo). Um projétil de fogo acende grama, a grama cria uma corrente de ar, a corrente
leva o planador ao topo da mesa. A mesma regra vale para inimigos, armas, clima e comida.

## 2. Pilares (da Bíblia)

1. **Gameplay multiplicativo**: poucas ferramentas genéricas que combinam entre si; o jogador inventa a solução.
2. **Mundo físico e químico**: todo objeto tem material e estado; elementos (fogo, gelo, raio, vento, água) mudam
   materiais; as regras valem igual para jogador, inimigos e cenário.
3. **Liberdade de exploração**: o mapa inteiro abre após a área inicial; curiosidade é recompensada, nunca bloqueada.

Princípios derivados: escassez cria decisão (armas quebram, stamina acaba, clima pune); descoberta sem tutorial;
silêncio como padrão; mesma regra para todos.

**Proibido (rejeição automática):** classes, nível de personagem, XP, árvore de talentos, loot com raridade por cor,
afixos de "+X% de dano", armas indestrutíveis (além da âncora), cura comprável, muro invisível, marcador de quest
para lugar não visto, puzzle com solução única, regra que vale só para o jogador.

## 3. Jogador

| Sistema | Regra | Valores |
|---|---|---|
| Vida | Corações em quartos; containers com 4 Orbes Espirituais; corações amarelos temporários de comida | 3 → 20 corações (12 → 80 pontos) |
| Stamina | Roda de 1000 unidades; Vessels de 200 (4 orbes); 3 rodas no máximo; zerar exaure até encher | Corrida 110/s, escalada 95/s (lateral 50 %), salto de escalada 150, nado 130/s, dash 200, planador 25/s, mira aérea 250/s; regen 300/s após 0,5 s |
| Movimento | Andar 4 m/s, correr 7 m/s, escalar 2 m/s, nadar 2,4 m/s | Chuva: escalada +20 % e escorregões |
| Queda | Sem dano até 6 m; 1 coração até 12 m; +1 por 3 m; morte a 30 m; planador aberto ou água funda anulam | |
| Dano recebido | `max(1, ataque − defesa)`; fogo, raio, queda e clima ignoram defesa | |
| Dano causado | ataque da arma × multiplicador × bônus Potente; furtivo ×8, congelado ×3, último golpe ×2 | |

## 4. Travessia

Escalar qualquer superfície íngreme, planar de qualquer altura (correntes de ar quente sobem), nadar, esquivar.
Esquiva no fim do telegrafo do inimigo = esquiva perfeita, câmera lenta e **Rajada** (ataques 3× mais rápidos).

## 5. Química do mundo

| Superfície | Fogo | Gelo | Raio | Vento |
|---|---|---|---|---|
| Grama | Queima, espalha e gera corrente de ar | — | — | Espalha fogo próximo |
| Grama queimando | — | Apaga | — | Espalha mais rápido |
| Água | — | Pilar de gelo (3 ativos no máximo, derrete) | Eletrifica (dano em quem está na água) | — |
| Gelo | Derrete | — | — | — |
| Metal | Aquece | — | Conduz para metais vizinhos (raio salta 3×) e ativa mecanismos | — |
| Criaturas | Queimam (1/4 por 0,5 s) | Congelam 1,2 s; próximo golpe ×3 | Paralisam 2 s e soltam a arma | Empurradas |

## 6. Runas (ferramentas permanentes, todas no início)

Brasa (fogo), Geada (gelo), Faísca (raio, salta por 3 metais), Lufada (vento). Dano em pontos de vida, recarga
de 1 a 2 s. Nenhuma runa nova surge no meio do jogo.

## 7. Armas, arcos e escudos

- Classes: uma mão (combo 4, Rajada 7), duas mãos (×1,5 ataque, combo 3, Rajada 4), lança (×0,7 ataque, ×1,3
  durabilidade, combo 5, Rajada 9), arco, escudo.
- Curva (uma mão): tier 0 a 5 = ataque 4/10/18/26/36/45, durabilidade 8/20/26/32/40/50. Vidro: 48/12.
  Âncora: Espada lendária 28 (56 contra corrupção), 40 de energia, recarrega em 8 min. Escudo lendário 80/600.
- Cada golpe que acerta consome 1 de durabilidade; o último golpe causa dano dobrado; arremesso quebra a arma
  (dano dobrado se estava gasta). Escudo bloqueia até sua guarda; defesa perfeita (pular com o escudo erguido)
  não gasta nada e atordoa.
- Modificadores só em armas do mundo (nunca de loja), a partir de 120 pontos de escala: Ataque+, Durabilidade+,
  Crítico, Arremesso longo, Tiro rápido, Tiro triplo, Guarda+.
- Bolsas: armas 6 → 16, arcos 4 → 10, escudos 3 → 14; 170 sementes Korok para o máximo.

## 8. Inimigos

Famílias: Cascudo (grunt), Brutamontes, Lagartão, Vigia (santuários), Juba de Ferro (elite), Gigante, Golem,
Sentinela. Tiers vermelho → azul → negro → prateado com HP ×3 (Cascudo 12/40/130/400; Brutamontes 45/120/300/900;
Juba de Ferro 1500/2250/3000). O tier define a arma carregada (0-1/2/3/4). Percebem por cone de visão e audição
(chuva reduz à metade); dormem à noite (21:00–05:00); deixam cair a arma e partes fixas.
**Escala de dificuldade**: cada morte soma pontos (até 10 por família); 60 → azul, 120 → modificadores,
180 → negro, 400 → prateado. **Lua de Sangue** a cada 5 dias à meia-noite: tudo renasce.

## 9. Materiais, culinária e economia

Até 5 ingredientes na panela; cura dobra ao cozinhar; um efeito por prato (dois diferentes se anulam); nível 1/2/3
por potência somada (1-2 / 3-4 / 5+; cinco iguais dão o máximo); durações base por efeito + 30 s por ingrediente extra, máx. 30:00; crítico
10 % (garantido na janela da Lua de Sangue 23:30–00:15). Criaturinha + parte de monstro = elixir; comida + parte
de monstro = duvidosa; minério ou madeira = dura como pedra. Efeitos: Robusto, Energizante, Resistente (stamina),
Picante, Gelado, Isolante, Ignífugo, Veloz, Furtivo, Potente, Resistente (defesa). **Não existe cura comprável.**
Rupees só compram armadura, flechas, cama e registro de cavalo; armas nunca.

## 10. Armaduras e acessórios

Três peças por conjunto; defesa por peça 3 → 5 → 8 → 12 → 20 nos 4 níveis de upgrade (materiais, nas Grandes
Fadas); bônus de conjunto com as 3 peças no nível 2+. Cada conjunto resolve UMA pressão do mundo (frio, calor,
fogo, nado, escalada, furtividade, sentinelas) e tem alternativa por comida e por comportamento. Oito conjuntos
no protótipo: campo, furtivo, penas (frio), deserto (calor), ígneo, águas, escalador, ancestral.

## 11. Clima e sobrevivência

Dia de 24 min reais (1 s = 1 min). Frio abaixo de 0 °C (nível 1) e −10 °C (nível 2); calor acima de 40 °C e
50 °C; 1/4 de coração a cada 5 s (nível 1) ou 2,5 s (nível 2); roupa ou comida do mesmo nível anula. Raios caem
em quem porta metal (aviso de 5 s, 5 corações, ignora armadura). Fogueira anula frio nível 1 por perto.

## 12. Mundo aberto (ilha de protótipo → 5 regiões)

Regra do triângulo em 3 escalas; pontos de interesse nos picos; nunca mais de 60–90 s sem algo novo à vista;
nenhum muro invisível (fronteiras naturais). Provas (santuários) dão 1 Orbe Espiritual; Estátua da Deusa troca
4 orbes por coração ou stamina. Meta de conteúdo: 5 regiões, 80 santuários, 300 sementes, 3 masmorras + fortaleza.

## 13. Direção de arte, áudio e HUD

Low-poly pintado com toon em faixas; paleta por região; silhueta legível a 50 m; código de cor funcional
(metal brilha, madeira opaca, explosivo vermelho, tecnologia ancestral ciano/laranja, tiers de inimigo por cor).
HUD: corações no canto superior esquerdo, runa/arma/arco/escudo abaixo, roda de stamina ao lado do personagem,
relógio e temperatura no canto inferior direito, prompts só com ação possível. Tudo tem que continuar jogável
com o "HUD mínimo" (só corações). Música esparsa; o som do mundo guia.

## 13b. Personagem e animação

- **Herói:** aventureiro élfico jovem no espírito da referência, sem copiar sua aparência: orelhas pontudas,
  cabelo louro com franja e rabo curto, brincos, túnica azul-céu sobre camisa creme, cinto e alça de couro,
  calça bege, botas e luvas marrons. Modelo gerado por `Tools/Blender/build_models.py` (`humanoid("heroi")`).
- **Equipamento visível:** espada, escudo e arco existem duas vezes no modelo (nas costas e nas mãos);
  `HeroPresenter` mostra uma versão ou outra conforme a bolsa e o estado (espada em punho por 4 s após um
  ataque, com alvo travado ou escudo erguido; escudo no braço ao defender; arco na mão ao mirar). A lâmina
  muda de proporção conforme a classe da arma (uma mão, duas mãos, lança).
- **Animações por ação** (`CharacterAnimator`, procedurais sobre o rig): golpe de uma mão (alterna lateral e
  de cima), golpe pesado de duas mãos, estocada de lança, arremesso, tiro, runa, dano, pegar objeto, defesa
  perfeita, aterrissagem pesada, salto de escalada, dash aquático; posturas mantidas: escudo erguido, mira com
  arco, ajoelhar (panela e estátua); locomoção: parado (olhar ao redor), andar/correr, pulo, escalada,
  planador, nado. Criaturas usam o mesmo animador com o golpe da arma que carregam.

## 14. Forma técnica

- Unity 6 (6000.6.4f1), Built-in RP no protótipo (migrar para URP antes do lançamento), Input System.
- Domínio em C# puro (`Zeldexile.Domain`, sem Unity, testado em EditMode): corações, stamina, armas, inventário,
  inimigos/escala, culinária, armaduras, clima, regras de dano. MonoBehaviours só fazem a ponte.
- Todo efeito elemental passa por um único `ImpactResolver`; golpes corpo a corpo reportam pelo mesmo evento.
- Compilação rápida do domínio fora do Unity: `Tools/DomainCheck` (dotnet build).

## 15. Decisões registradas

| Data | Decisão |
|---|---|
| 16/10/2026 | Projeto novo e independente, em `D:\19 - Zeldexile`. |
| 16/10/2026 | Visão 2.5D em terceira pessoa; Unity 6000.6.4f1 com Built-in RP; personagens low-poly com toon. |
| 16/10/2026 | Regra de arte: polígonos otimizados e arestas chanfradas; modelos do Blender por script. |
| 17/10/2026 | Pesquisa de referência aplicada: toon v2, atmosfera, grama, clima, esquiva perfeita, furtividade. |
| 17/10/2026 | **Adotada a Bíblia de Design no formato de Breath of the Wild como padrão único.** Removidos: raridades, afixos, encaixes/elos, crafting de rerrola e árvore passiva (proibidos pela Bíblia). Entraram: corações e orbes, roda de stamina de 1000, armas com durabilidade e bolsas, inimigos por família/tier com escala de dificuldade e Lua de Sangue, culinária, armaduras por pressão do mundo, calor, queda, escudo/defesa perfeita, arremesso, arco, objetos interativos (panela, estátua, armas no chão). |
| 17/10/2026 | Herói redesenhado (silhueta élfica, túnica azul, equipamento visível nas costas e nas mãos) e animações distintas por ação; a folha de poses (`Logs/preview-poses.png`) mostra todas. |
| 17/10/2026 | Custos de escalada (95/s a 2 m/s) e nado (130/s a 2,4 m/s) calibrados para a ilha atual (Mesa Alta de 26 m e lago continuam fora do alcance de uma roda); a aba "Valores propostos" da Bíblia traz 70/s e 60/s como alvo para o mapa final. |
| 17/10/2026 | Herói jogável refeito a partir da referência de seis poses (`Docs/Personagem-Aventureiro.md`) e ganhou esqueleto de 26 ossos com segmentos rígidos: coluna em dois segmentos, pescoço, mandíbula, clavículas, cotovelos, pulsos, dedos, joelhos, tornozelos, pontas dos pés e bolsa secundária. Pés plantados e altura do quadril vêm das pernas. Ossos de torção, rig facial e falanges ficaram de fora por não terem efeito numa malha rígida low-poly. |
| 17/10/2026 | **Primeira região do mundo aberto: Vale dos Pomares** (1 km², 25 setores de 200 m com ids estáveis). Região descrita em dados (`Resources/World/Regions/vale_inicial.json`) e montada em tempo de execução com sementes fixas; vizinhos futuros se encaixam pelo contrato de borda. Natureza instanciada a partir de modelos do Blender. Ver [Regiao-Vale-Inicial.md](Regiao-Vale-Inicial.md). |
