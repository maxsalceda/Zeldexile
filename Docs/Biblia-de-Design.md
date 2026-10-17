<!-- Exportado do Claude Doc https://claude.ai/code/artifact/a37f7bd0-7e67-4e64-8dee-852e05bb0b9a em 17/10/2026. O documento vivo é o Claude Doc; este arquivo é uma cópia para o repositório. -->

# Bíblia de Design — Padrão Breath of the Wild

Oct 17, 2026 · @Max

## Propósito e regra de ouro

Este documento é a referência única de design do jogo: toda mecânica, personagem, item, acessório, inimigo, região ou tela deve ser derivada do padrão de design de *The Legend of Zelda: Breath of the Wild* (Nintendo, 2017) descrito aqui. Nada entra no jogo sem passar pelos templates da seção "Templates obrigatórios" e pelo checklist final.

**Regra de ouro:** se um elemento não puder ser explicado em termos de *exploração livre*, *interação física/química sistêmica* e *ferramentas que se multiplicam entre si*, ele não pertence a este jogo.

**Como usar:**

1. Leia os Pilares. Eles são o filtro de tudo.
2. Consulte a seção do sistema que o novo elemento toca (combate, movimentação, itens, etc.).
3. Preencha o template correspondente da seção "Templates obrigatórios".
4. Passe pelo checklist e pela lista de proibições antes de aprovar.

As regras de referência (durabilidade, stamina, escala de inimigos, etc.) citam os valores reais de Breath of the Wild como *baseline*. O jogo pode ajustar números, nunca a lógica por trás deles.

Os números específicos deste jogo, derivados dessas curvas, estão na aba Valores propostos.

## Pilares de design

Os três pilares vêm da palestra da equipe de Breath of the Wild na GDC 2017 (Hidemaro Fujibayashi, diretor; Takuhiro Dohta, diretor técnico; Satoru Takizawa, diretor de arte): *multiplicative gameplay*, um mundo regido por física e química, e a quebra das convenções lineares da série ([Tom's Guide](https://www.tomsguide.com/uk/us/zelda-breath-wild-design,news-24586.html), [Thumbsticks](https://www.thumbsticks.com/gdc-17-breath-of-the-wild-science-lies)).

| Pilar | Definição | Teste de aceitação para qualquer elemento novo |
| --- | --- | --- |
| **1. Gameplay multiplicativo** | Poucas ferramentas genéricas que combinam entre si geram muitas soluções; o jogador inventa a resposta, o design não a prescreve. | O elemento interage com pelo menos 2 sistemas já existentes? Se só serve para uma situação, é rejeitado. |
| **2. Mundo físico e químico** | Todo objeto tem massa, material e estado (fogo, gelo, eletricidade, vento, água). As regras valem igual para jogador, inimigos e cenário. | O elemento obedece às tabelas de material e estado da seção de Runas e química? Ele reage e provoca reações? |
| **3. Liberdade de exploração** | O mapa inteiro está aberto após o tutorial. O jogador escolhe a ordem, o caminho e até ir direto ao chefe final. Curiosidade é recompensada, nunca bloqueada por muro invisível. | O elemento pode ser encontrado em qualquer ordem e sem pré-requisito de história? Ele usa o olhar (ver ao longe) como gatilho? |

**Princípios derivados (obrigatórios):**

- **Active play, não passive play.** Fujibayashi descreve a meta como sair de um jogo onde se joga dentro de um mecanismo pré-montado para um onde o jogador age sobre o mundo. Nenhum puzzle deve ter uma única solução intenção-do-designer.
- **Mesmas regras para todos.** Inimigos pegam fogo, escorregam no gelo, são atingidos por raios e lançados pelo vento exatamente como Link. Uma regra que vale só para o jogador é proibida.
- **Triângulos e gravidade do olhar.** O terreno é desenhado em formas triangulares que escondem e revelam pontos de interesse; o jogador vê algo ao longe, vai até lá e no caminho descobre outra coisa (ver seção Mundo aberto).
- **Escassez cria decisão.** Armas quebram, stamina acaba, clima pune. Cada recurso finito força uma escolha; nenhum sistema entrega poder permanente de graça.
- **Descoberta sem tutorial.** O jogo ensina pelo cenário (um Bokoblin cozinhando ensina culinária; uma árvore caída ensina ponte). Texto explicativo é último recurso.
- **Silêncio como padrão.** Música é esparsa (piano pontual) para que o som do mundo guie o jogador; UI mínima e diegese alta.

**O que isso exclui:** classes, árvores de talentos, nível de personagem, loot com raridade por cor, marcadores de quest no mapa sem o jogador os ter visto, portas trancadas por chave de história.

## Análise das prints de gameplay

As dez prints fornecidas cobrem os sete verbos centrais do jogo: cavalgar, contemplar, planar, explodir, lutar, escalar e conversar. Cada cena abaixo vira uma regra de design.

| # | Cena | O que a print mostra | Regra derivada para este jogo |
| --- | --- | --- | --- |
| 1 | Link a cavalo em Hyrule Field, castelo ao fundo | 4 ícones de estímulo (spur) sob o cavalo, HUD com 22 corações, relógio, temperatura, minimapa circular, prompts contextuais na base | O cavalo tem gauge próprio de estímulos (5 máx.) que recarrega; o castelo é visível de quase qualquer ponto do mapa como âncora de orientação; prompts de botão mudam com o contexto |
| 2 | Link no Grande Platô olhando Hyrule (abertura) | Sem HUD, lança nas costas, montanhas em camadas, neblina de distância | Vistas panorâmicas são momentos de design: a câmera recua, a UI some e o jogador escolhe o destino pelo olhar |
| 3 | Link planando sobre caveira de Bokoblin (acampamento) | Paraglider aberto, 4 corações, acampamento inimigo visto de cima com baú dentro da caveira | Acampamentos são estruturas legíveis à distância; o paraglider permite reconhecimento aéreo e ataque por cima |
| 4 | Explosão de barris vermelhos em acampamento | Bomba selecionada na slate, barris explosivos, torre de vigia de madeira, 15 flechas | Objetos explosivos são peças do cenário, não itens: o jogador combina fogo/flecha/bomba com o que o mundo oferece |
| 5 | Bokoblin saltando com arma, Link com escudo | Seta laranja de alvo (lock-on), Bokoblin azul e vermelho, 3 corações, Magnesis ativa | Inimigos têm cor como tier; o lock-on marca o alvo; inimigos usam as mesmas armas que o jogador pode pegar |
| 6 | Floresta nebulosa com Stalfos/estalactites de fogo (Lost Woods) | HUD de materiais à esquerda (x1, x3), tochas, neblina densa, 11 corações | Regiões têm identidade de iluminação e atmosfera; a luz/fogo é ferramenta de navegação |
| 7 | Link na colina de outono (Akkala), espada e escudo | Sem HUD, folhagem vermelha, luz de fim de tarde | Cada região tem paleta e estação próprias; o ciclo diário muda a cor do mundo |
| 8 | Link escalando parede rochosa, vilarejo ao fundo | Prompts Jump (X) / Let go (B), stamina visível, 3 corações, torre ao longe | Qualquer superfície é escalável; stamina limita; a torre é o ponto de interesse de longa distância |
| 9 | Link em Kakariko com máscara, NPCs | Vila com bandeirinhas, NPCs com rotina, sapos de pedra, abobrinhas | Vilas são espaços seguros com horário e rotina de NPCs; roupas e máscaras mudam como NPCs e inimigos reagem |
| 10 | Link correndo de Guardian atirando laser | Raio azul de Guardian, lock-on, 3 corações, Guardian com pernas e olho | Inimigos de elite são legíveis (som + luz antes do disparo) e podem matar com um golpe no início; fugir é resposta válida |

**Invariantes de HUD vistas em todas as prints (ver seção Arte, áudio e interface):** corações no canto superior esquerdo; runa/arma/escudo em ícones pequenos abaixo; minimapa circular no canto inferior direito com relógio, termômetro e sensor; prompts de botão centralizados na base só quando há ação contextual.

## Mundo aberto e exploração

O mundo é um único mapa contínuo, sem telas de carregamento entre regiões, totalmente acessível depois da área tutorial (o Grande Platô). Toda a progressão espacial é feita pelo olhar: ver, querer ir, chegar, ver outra coisa.

**Estrutura de referência (Breath of the Wild):**

| Elemento | Quantidade | Função de design |
| --- | --- | --- |
| Regiões de torre | 15 (Akkala, Central, Dueling Peaks, Eldin, Faron, Gerudo, Great Plateau, Hateno, Hebra, Lake, Lanayru, Ridgeland, Tabantha, Wasteland, Woodland) | Cada torre revela o mapa da sua região, é visível de longe e serve de mirante e ponto de teletransporte ([Zelda Dungeon](https://www.zeldadungeon.net/wiki/Korok_Seed)) |
| Santuários | 120 (+16 no DLC) | Micro-desafios de 3 a 10 minutos; cada um dá um Spirit Orb e vira ponto de viagem rápida ([Zelda Dungeon](https://www.zeldadungeon.net/?p=91923)) |
| Sementes Korok | 900 (441 bastam para o inventário máximo) | Micro-puzzles ambientais de 10 a 30 segundos que recompensam curiosidade em qualquer ponto do mapa ([Zelda Dungeon](https://www.zeldadungeon.net/wiki/Korok_Seed)) |
| Masmorras principais | 4 Divine Beasts + Castelo de Hyrule | Podem ser feitas em qualquer ordem; o castelo pode ser atacado a qualquer momento |
| Estábulos | 15 | Pontos seguros com cama, cavalo, NPCs viajantes e mercador Beedle |
| Vilas | Kakariko, Hateno, Zora's Domain, Goron City, Rito Village, Gerudo Town, Lurelin, Tarrey Town, Korok Forest | Cada uma é um hub cultural de uma raça, com loja, lojas de armadura e quests |

**Regra do triângulo.** O terreno é composto por formas triangulares (montanhas, colinas, rochas) em três escalas: grande (montanhas que bloqueiam a vista e forçam decisão de rota), média (colinas que escondem um acampamento ou santuário até o jogador passar) e pequena (rochas e árvores que escondem um Korok). O jogador sempre decide: contornar (mais lento, mais descobertas) ou escalar (gasta stamina, ganha vista). Pontos de interesse ficam nos picos, onde o olho pousa ([Source Gaming](https://sourcegaming.info/2017/11/25/holism-breath-of-the-wilds-golden-triangles/), [80.lv](https://80.lv/articles/the-design-secrets-of-breath-of-the-wild/?amp=1)).

**Gravidade do mapa (pontos e linhas).** As torres são os pontos âncora; entre dois pontos há sempre uma linha de eventos: um acampamento, um viajante, um Korok, uma ruína, um santuário. A densidade segue a regra "nunca mais de 1 a 2 minutos sem algo novo no campo de visão" ([GMTK](https://gmtk.substack.com/p/how-nintendo-solved-zeldas-open-world)).

**Hierarquia de legibilidade à distância (obrigatória para todo novo ponto de interesse):**

1. Visível do mapa inteiro: Castelo de Hyrule (âncora central), Death Mountain (fogo), Divine Beasts (silhueta animal), Dragons.
2. Visível da região: torres (luz laranja/azul), santuários (brilho laranja antes de ativar, azul depois), Great Fairy Fountains (botão gigante), estábulos (cabeça de cavalo no telhado).
3. Visível de perto: acampamentos (fumaça, caveira), ruínas, flores raras, círculos de pedras, Koroks.

**Regras obrigatórias para qualquer região nova:**

- Tem um bioma dominante com um sistema de sobrevivência próprio (frio, calor, fogo, raio, areia, neve).
- Tem uma raça ou cultura residente com arquitetura, roupa e música distintas.
- Tem um ponto alto de orientação e um corpo d'água ou vale como fronteira natural.
- Tem ao menos: 1 torre, 6 a 10 santuários, 1 estábulo, 2 a 4 acampamentos, 20+ Koroks, 1 inimigo de elite (Lynel, Hinox, Talus ou Molduga).
- Nenhum muro invisível: fronteiras são oceano, abismo ou montanha escalável.

**Viagem rápida.** Qualquer torre ou santuário ativado vira ponto de teletransporte. A viagem rápida nunca leva a um lugar que o jogador não tenha alcançado a pé primeiro.

**Mapa e marcadores.** O mapa só revela o relevo da região cuja torre foi ativada. O jogador coloca até 100 pins e até 5 beacons próprios; a luneta (scope) permite marcar o que se vê à distância. Quests marcam no mapa apenas lugares já descritos pelo NPC.

## Movimentação e sobrevivência

A stamina é o recurso que converte terreno em decisão: tudo que vence a gravidade ou a água a consome, e ela se recupera sozinha em poucos segundos quando o jogador para.

**Roda de stamina**

| Parâmetro | Valor de referência | Fonte |
| --- | --- | --- |
| Roda inicial | 1 roda completa | [Zelda Wiki](https://zeldawiki.wiki/wiki/Stamina_Wheel) |
| Upgrade | 1 Stamina Vessel = 1/5 de roda; custa 4 Spirit Orbs numa Estátua da Deusa | [Zelda Wiki](https://zeldawiki.wiki/wiki/Stamina_Vessel) |
| Máximo | 3 rodas (10 vessels) | [Zelda Wiki](https://zeldawiki.wiki/wiki/Stamina_Vessel) |
| Consome | Correr, escalar, nadar, planar, ataques carregados, esporear o cavalo | [Zelda Wiki](https://zeldawiki.wiki/wiki/Stamina_Wheel) |
| Zero stamina | Escalando: cai. Nadando: afoga (perde corações e volta à margem). Planando: fecha o paraglider e cai. Correndo: para ofegante por \~2 s | [Zelda Wiki](https://zeldawiki.wiki/wiki/Stamina_Wheel) |
| Stamina extra (amarela) | Comida "Enduring" adiciona roda temporária amarela acima do máximo; some ao ser gasta | [Zelda Wiki](https://zeldawiki.wiki/wiki/Ingredients) |
| Troca | A Estátua Chifruda (Hateno) compra um Vessel/Container por 100 rupees e vende de volta por 120 | [Zelda Wiki](https://zeldawiki.wiki/wiki/Heart_Container) |

**Escalada.** Toda superfície sólida é escalável, exceto paredes de santuário e Divine Beasts (material liso) e superfícies molhadas por chuva, onde Link escorrega a cada 3 passos. Pular durante a escalada gasta um bloco grande de stamina e avança mais rápido. A velocidade de escalada sobe com o conjunto Climber's e o custo cai com o bônus de set. A chuva não é bug: é a regra que empurra o jogador a esperar numa fogueira, procurar outro caminho ou cozinhar.

**Paraglider.** Obtido no fim do tutorial, é a única ferramenta de travessia permanente. Consome stamina lentamente; correntes ascendentes (fogo, grama queimando, ventos de montanha, Revali's Gale) devolvem altura sem custo. Pousar de qualquer altura com o paraglider aberto nunca causa dano. Cair sem ele acima de \~6 m causa dano por altura.

**Corrida, salto e queda.** Corrida gasta stamina, caminhar não. Salto tem altura fixa. Dano de queda escala com altura e mata se for alta o bastante; a água funda anula dano de queda.

**Nado.** Consome stamina continuamente; dash na água gasta mais. Cachoeiras só podem ser subidas com a Zora Armor. Afogar-se tira um coração e devolve Link à margem.

**Cavalo.**

- Selvagens em manadas; domar exige aproximação furtiva, montar e acalmar (L) gastando stamina: cavalos comuns pedem \~15 toques, o Cavalo Branco Real 25 e o Cavalo Gigante 30 ([Zelda Dungeon](<https://www.zeldadungeon.net/wiki/Horse_(Breath_of_the_Wild)>)).
- Atributos: força, velocidade, stamina (número de esporadas, 1 a 5), temperamento (dócil ou selvagem) e vínculo (0 a 100; +3 ao acalmar após galope, +5 após correção, +10 por item de comida) ([Zelda Dungeon](<https://www.zeldadungeon.net/wiki/Horse_(Breath_of_the_Wild)>)).
- Registro em estábulo custa 20 rupees; até 5 cavalos registrados; cavalo pode morrer e ser ressuscitado pela Horse God Malanya.
- A cavalo, Link pode atacar com armas, atirar com arco em câmera lenta e atropelar inimigos pequenos. O cavalo segue estradas sozinho se o jogador soltar os controles.

**Clima e temperatura.** O mundo tem relógio de 24 h (1 min real = 1 h de jogo, dia completo = 24 min reais), previsão no HUD e clima por região (sol, nuvem, chuva, tempestade com raios, neve, tempestade de areia).

| Condição | Gatilho | Efeito | Contramedidas (nível 1 e 2) |
| --- | --- | --- | --- |
| Frio nível 1 / 2 | Abaixo de 0 °C / abaixo de -10 °C | Perde corações aos poucos / mais rápido | Roupa quente (Warm Doublet, Snowquill, Ruby Circlet), comida Spicy, segurar arma de fogo, ficar perto de fogueira ([Zelda Wiki](https://zeldawiki.wiki/wiki/Temperature)) |
| Calor nível 1 / 2 | Acima de 40 °C / acima de 50 °C | Perde corações aos poucos / mais rápido | Desert Voe, Gerudo Vai, Sapphire Circlet, comida Chilly, sombra, água, noite ([Zelda Wiki](https://zeldawiki.wiki/wiki/Temperature)) |
| Calor escaldante | Eldin / Death Mountain | Link e itens de madeira pegam fogo | Flamebreaker set ou elixir Fireproof |
| Chuva | Aleatória por região | Escalada escorrega, fogo apaga, inimigos ouvem menos, raios | Esperar, fogueira passa o tempo |
| Tempestade com raios | Chuva forte | Raio cai em quem porta metal (arma, escudo ou arco metálico) em \~10 s de aviso (faíscas) | Desequipar metal; usar raio contra inimigos soltando metal perto deles |
| Água gelada | Hebra, Lanayru | Dano rápido, sem proteção possível | Cryonis ou contornar |

**Regra de design:** cada bioma tem UM sistema de pressão de sobrevivência e TRÊS caminhos para resolvê-lo: equipamento (permanente, caro), consumível (temporário, barato) e comportamento (grátis, exige leitura do mundo).

## Combate

O combate é um sistema de economia: toda arma quebra, então o jogador decide o que gastar em cada luta, e o mundo (física, química, terreno) é sempre uma arma alternativa.

**Classes de arma**

| Classe | Slot | Velocidade | Alcance | Combo normal | Flurry Rush | Ataque carregado | Observações |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Uma mão (espada, clava, machadinha) | Arma + escudo | Rápida | Curto | 3 a 4 golpes | 7 golpes | Giro 360° (gasta stamina) | Permite escudo e parry |
| Duas mãos (claymore, martelo, machado) | Arma (sem escudo) | Lenta | Médio | 3 golpes | 4 golpes | Giro contínuo (gasta stamina) | Maior dano e knockback; quebra guarda |
| Lança / cabo longo | Arma + escudo | Muito rápida | Longo | 5 estocadas | 9 golpes | Investida com stamina | Menor dano por golpe |
| Arco | Arco | Varia (puxada) | Longo | 1 flecha (ou multi-shot) | Não | Mirar | Câmera lenta ao atirar no ar (gasta stamina) |
| Escudo | Escudo | Instantânea | Corpo | Bloqueio / parry | Não | Shield surf | Perde durabilidade ao bloquear e surfar |

Contagens de Flurry Rush por classe segundo a [Zelda Wiki](https://zeldawiki.wiki/wiki/Flurry_Rush).

**Durabilidade** ([Zelda Wiki](https://zeldawiki.wiki/wiki/Durability), [RPG Site](https://rpgsite.net/feature/5365-the-legend-of-zelda-breath-of-the-wild-guide-tips-on-weapons-durability-and-combat))

- Cada arma tem ataque e durabilidade inteiros; cada golpe que acerta algo sólido consome 1 ponto. Golpes no ar não consomem.
- Quando resta pouca durabilidade, a arma pisca vermelha no menu e o jogo avisa "badly damaged".
- O último golpe antes de quebrar causa dano em dobro (crítico garantido).
- Arremessar uma arma causa dano e a quebra imediatamente se acertar inimigo; arremesso de arma vermelha é crítico garantido.
- Escudos perdem durabilidade ao bloquear golpes e ao surfar em terreno áspero; parry perfeito não consome.
- A Master Sword não quebra: perde energia e recarrega em 10 minutos reais; ataque 30, dobrado para 60 perto de corrupção (Guardians, Divine Beasts, castelo).
- Armas compradas em loja nunca recebem modificadores; armas do mundo podem vir com modificadores (Attack Up, Durability Up, Critical Hit, Long Throw, Quick Shot, Multi-Shot, Shield Guard Up) em dois níveis: azul/branco e amarelo ([ZeldaMods](https://zeldamods.org/wiki/Difficulty_scaling)).

**Valores de referência de armas** ([Gamer Guides](https://www.gamerguides.com/the-legend-of-zelda-breath-of-the-wild/guide/weapons-and-armor), [Zelda Dungeon](https://zeldadungeon.net/wiki/One-Handed_Weapon))

| Arma | Ataque | Durabilidade | Papel |
| --- | --- | --- | --- |
| Rusty Broadsword | 6 | 8 | Lixo de início, troca rápida |
| Knight's Broadsword | 26 | 27 | Padrão de meio de jogo |
| Master Sword | 30 (60) | 40 (recarrega) | Arma âncora |
| Royal Broadsword | 36 | 36 | Topo das de uma mão |
| Royal Guard's Sword | 48 | 14 | Vidro: dano altíssimo, quebra rápido |
| Royal Claymore | 52 | 40 | Topo das de duas mãos |
| Ancient Bladesaw | 55 | 50 | Melhor contra Guardians |
| Hylian Shield | 90 (defesa) | 800 | Escudo âncora |

A curva: ataque sobe linearmente por tier (6 → \~15 → \~26 → \~36 → \~50) e a durabilidade acompanha, com exceções deliberadas de "vidro" (Royal Guard) e "âncora" (Master Sword, Hylian Shield).

**Inventário** ([Zelda Wiki](https://zeldawiki.wiki/wiki/Korok_Seed)): armas começam em 8 slots e vão a 20; arcos de 5 a 13 (o 14º é flechas); escudos de 4 a 20. Cada slot custa sementes Korok em progressão crescente (1, 2, 3, 5, 8, 12, 17, 25, 35, 45, 55 para armas), total 441 sementes.

**Técnicas defensivas**

- **Perfect dodge (esquiva perfeita):** pular para o lado (golpe vertical) ou para trás (golpe horizontal) no último instante; ativa câmera lenta e o Flurry Rush. Só funciona contra ataques corpo a corpo ([Zelda Wiki](https://zelda.fandom.com/wiki/Perfect_Dodging)).
- **Perfect guard (parry):** segurar escudo e apertar A no instante do impacto; reflete projetos, inclusive o laser do Guardian de volta nele. Não consome durabilidade.
- **Lock-on (ZL):** fixa alvo, mostra seta sobre ele (print 5), muda movimento para lateral.
- **Furtividade:** medidor de ruído no HUD; agachar, roupas Stealth e comida Sneaky reduzem; ataque furtivo por trás causa 8x dano.

**Tipos de dano e flechas:** normal, fogo (queima madeira e grama, derrete gelo, explode barris), gelo (congela inimigo; golpe seguinte é crítico x3), elétrico (paralisa e derruba armas de metal), bomba (explosão em área), ancient (dano massivo em Guardians, 1 flecha derruba um Stalker no olho). Headshots causam crítico.

**Montaria em combate:** Lynels e cavalos permitem montar e golpear sem gastar durabilidade ([Zelda Wiki](https://zeldawiki.wiki/wiki/Durability)).

**Regra de design:** cada luta deve ter ao menos uma solução sem gastar arma (física, fogo, barril, queda, furtividade, fuga). Se um encontro só é resolvível com DPS, está errado.

## Runas da Sheikah Slate e sistema de física/química

O jogador recebe todas as ferramentas principais nas primeiras 2 horas (4 santuários do Grande Platô) e nunca ganha uma nova habilidade depois; toda a profundidade vem da combinação delas com o mundo.

**Runas** ([Zelda Dungeon](https://www.zeldadungeon.net/wiki/Remote_Bomb), [Zelda Wiki](https://zeldawiki.wiki/wiki/Rune))

| Runa | Função | Valores de referência | Upgrade (Hateno Tech Lab) | Usos multiplicativos |
| --- | --- | --- | --- | --- |
| Magnesis | Levanta e move qualquer objeto metálico | Alcance \~20 m; velocidade de arrasto limitada pela massa | Não tem | Ponte com placa, derrubar caixa em inimigo, pescar baú, mover barril de raio |
| Remote Bomb (redonda e cúbica) | Bomba infinita detonada à distância | Dano 12; recarga 6 s; raio de explosão fixo; redonda rola, cúbica fica parada | Bomb+: dano 24, recarga 3 s (custa 3 Ancient Shafts) | Minerar, abrir paredes rachadas, lançar com vento, bomb-jump, explodir barris |
| Stasis | Congela um objeto no tempo; golpes acumulam energia cinética que é liberada ao fim | Duração \~5 s; recarga \~10 s | Stasis+: funciona em inimigos (congela \~3 s, menos em elites) | Lançar rocha como projétil, parar engrenagem, viajar em cima de objeto lançado, congelar Guardian |
| Cryonis | Cria pilar de gelo em superfície de água | Até 3 pilares ativos; altura \~3 m; pilares podem ser quebrados com nova ativação ou bomba | Não tem | Subir cachoeira (pilar sob queda), levantar portão, escudo contra flechas, isolar inimigo na água |
| Camera + Hyrule Compendium | Fotografa e cataloga criaturas, materiais, equipamento, tesouros | 385 entradas no compendium | Não tem | Localizar materiais com o Sensor+ |
| Sheikah Sensor | Apita quando há santuário por perto | Raio curto, intensidade por distância | Sensor+: busca qualquer entrada do compendium | Caça de materiais e tesouros |

**Motor de química (regra de Takuhiro Dohta, GDC 2017)** ([Thumbsticks](https://www.thumbsticks.com/gdc-17-breath-of-the-wild-science-lies))

- *Física* governa movimento (corpos rígidos, Havok). *Química* governa mudança de estado.
- *Elementos* são não-sólidos: fogo, água, gelo, eletricidade, vento. *Materiais* são sólidos: madeira, metal, pedra, grama, carne, tecido.
- Três regras: (1) elementos mudam o estado de materiais; (2) elementos mudam o estado de outros elementos; (3) materiais não mudam o estado de outros materiais.

**Matriz obrigatória elemento × material (todo objeto novo preenche uma linha):**

| Material | Fogo | Gelo | Eletricidade | Vento | Água |
| --- | --- | --- | --- | --- | --- |
| Madeira (armas, caixas, árvores) | Queima e some; gera corrente ascendente | Nada | Nada (isolante) | Objetos leves voam | Apaga fogo nela |
| Metal (armas, placas, caixas) | Nada; conduz calor para Link | Nada | Conduz: paralisa quem segura, atrai raios | Nada | Nada |
| Pedra (rochas, Talus) | Nada | Nada | Nada | Nada | Nada |
| Grama / folhas | Queima em propagação, gera corrente ascendente | Nada | Nada | Balança; folhas voam | Molhada, não queima |
| Carne (Link, inimigos, animais) | Queima: dano contínuo, derruba arma de madeira | Congela: imobiliza; próximo golpe é crítico | Paralisa; derruba arma/escudo de metal | Empurra (bombas, Korok leaf) | Molha; apaga fogo |
| Explosivo (barris, bombas) | Explode | Nada | Explode | Rola | Nada |
| Gelo (Cryonis, blocos) | Derrete | Nada | Nada | Nada | Nada |

**Elemento × elemento:** fogo + gelo = água; fogo + vento = fogo se espalha na direção do vento; água + eletricidade = eletricidade se espalha pela superfície; água + fogo = vapor (fogo apaga); chuva apaga qualquer fogo exposto.

**Regra de design:** nenhum objeto é "cenário". Tudo tem material, massa e estado. Um objeto que não reage a nenhum elemento só é permitido se for arquitetura fixa (chão, paredes de santuário).

## Inimigos e bestiário

Poucas espécies, muitas variações: o bestiário tem cerca de 10 famílias, e a dificuldade vem de cor (tier), elemento, arma empunhada e contexto (acampamento, montaria, hora do dia), nunca de nível numérico visível.

**Escala de cor (tier).** Toda espécie comum segue a mesma ordem: vermelho (base) → azul → preto → prata (→ dourado só no Master Mode). O HP cresce \~3x a cada tier e o tier define que arma o inimigo carrega.

**Tabela de HP (datamine do jogo)** ([GitHub, m-byte918](https://gist.github.com/m-byte918/1d1acc569ad1cd59ead8e151fddf4591), [GameFAQs](https://gamefaqs.gamespot.com/switch/189707-the-legend-of-zelda-breath-of-the-wild/faqs/75488/enemy-statistics))

| Família | Base | Azul | Preto | Prata | Papel de design |
| --- | --- | --- | --- | --- | --- |
| Bokoblin | 13 | 72 | 240 | 720 | Inimigo-aula: ensina armas, acampamentos, furtividade |
| Moblin | 56 | 144 | 360 | 1080 | Tanque lento, arremessa Bokoblins |
| Lizalfos (e variantes fogo/gelo/elétrica 160/288/288) | 50 | 120 | 288 | 864 | Rápido, camuflado, cospe elemento |
| Lynel (vermelho / azul / juba branca / prata) | 2000 | 3000 | 4000 | 5000 | Mini-chefe de elite; teste de maestria |
| Hinox (vermelho / azul / preto) | 600 | 800 | 1000 | — | Gigante dorminhoco; puzzle de olho e colar de armas |
| Stalnox | 1000 | — | — | — | Versão noturna do Hinox |
| Stone Talus (comum / luminoso / raro) | 300 | 600 | 900 | — | Chefe de terreno; mineiro; Igneo e Frost 800 |
| Molduga | 1500 | — | — | — | Chefe de areia; detecção por vibração |
| Guardian Stalker / Skywatcher / Turret | 1500 | — | — | — | Terror móvel; laser one-shot no início |
| Decayed Guardian | 500 | — | — | — | Torreta fixa, ensina parry |
| Guardian Scout I / II / III / IV | 13 | 100 | 1500 | 3000 | Teste de combate dentro de santuários |
| Wizzrobe (fogo/gelo/elétrico; Meteo/Blizz/Thunder) | 150 | 300 | — | — | Mago volante que muda o clima local |
| Chuchu (3 tamanhos) | 3 | 20 | 48 | — | Gel elemental; drops de geléia |
| Keese (normal/fogo/gelo/elétrico) | 1 | — | — | — | Enxame noturno |
| Octorok (água/rocha/neve/floresta/tesouro) | 8 | — | — | — | Emboscada por camuflagem |
| Pebblit (pedra/igneo/frost) | 20 | — | — | — | Talus bebê |
| Yiga Footsoldier / Blademaster / Kohga | 72 | 400 a 600 | 300 (Kohga) | — | Inimigo humano; emboscada disfarçada de viajante |
| Stal- (Bokoblin, Moblin, Lizalfos) | 1 | — | — | — | Só à noite; morrem com um golpe mas se remontam |
| Blight Ganon (4) | 800 | +400 por Blight já vencido | — | — | Chefe de Divine Beast; HP escala com progresso ([ZeldaMods](https://zeldamods.org/wiki/Difficulty_scaling)) |
| Calamity Ganon / Dark Beast | 8000 | — | — | — | Chefe final; HP cai se os 4 Blights foram vencidos |

**Comportamentos obrigatórios de IA (todas as famílias comuns):**

- Rotina de dia: patrulha, dorme, cozinha, dança, caça. Rotina de noite: mais Stal e Keese, Bokoblins dormem no acampamento.
- Percepção por visão (cone) e audição (medidor de ruído do jogador); chuva reduz audição. Ao ver Link, tocam corneta e acordam o acampamento.
- Pegam qualquer arma do chão, inclusive as que Link larga; pegam fogo, congelam, levam choque e caem de penhasco como o jogador.
- Montam cavalos e javalis; atiram flechas elementais; usam o terreno (empurram pedras, jogam bombas).
- Deixam cair materiais fixos (chifre, presa, glândula) e a arma que carregam.

**Escala de dificuldade (World Level)** ([ZeldaMods](https://zeldamods.org/wiki/Difficulty_scaling)): o jogo soma pontos por cada espécie derrotada (contador por tipo até 10) e, ao atingir limiares, promove inimigos ao tier seguinte e troca armas de spawn por versões superiores (Soldier's Bow → Royal Bow). Não há nível do jogador: o mundo endurece porque o jogador o venceu.

**Lua de Sangue** ([ZeldaMods](https://zeldamods.org/wiki/Blood_moon)): a cada \~7 dias de jogo (timer de tempo real jogado, \~2 h 48 min), à meia-noite, todos os inimigos e armas de mundo renascem. Cozinhar entre 23:30 e 00:15 na Lua de Sangue garante acerto crítico.

**Template de inimigo (resumo; completo na seção Templates):** família, tier, HP, arma portada, elemento, percepção, rotina dia/noite, reações da matriz química, drops, acampamento típico, leitura à distância (silhueta, som, luz).

## Itens, materiais, culinária e economia

Não existem poções de loja nem cura automática: toda recuperação e todo buff vêm de materiais colhidos no mundo e transformados numa panela. Colher é explorar; cozinhar é planejar.

**Categorias de material**

| Categoria | Exemplos | Uso primário | Uso secundário |
| --- | --- | --- | --- |
| Frutas e vegetais | Apple, Hearty Durian, Endura Carrot, Hydromelon, Spicy Pepper | Comida (corações + efeito) | Isca para cavalo e animais |
| Cogumelos | Hylian Shroom, Stamella, Rushroom, Silent Shroom | Comida com efeito | Venda |
| Carnes e peixes | Raw Meat, Gourmet Meat, Hyrule Bass, Hearty Salmon | Comida com muitos corações | Assar direto no fogo (sem panela) |
| Ervas e flores | Hyrule Herb, Cool Safflina, Warm Safflina, Silent Princess | Efeito térmico/furtivo | Quest |
| Critters (insetos, lagartos, sapos) | Bokoblin Guts não; Hot-Footed Frog, Restless Cricket, Fireproof Lizard | Elixir (com parte de monstro) | Compendium |
| Partes de monstro | Bokoblin Horn/Fang/Guts, Lizalfos Tail, Lynel Hoof, Ancient Screw/Core | Elixir, upgrade de armadura, loja de Kilton | Rock-Hard Food se cozinhar sozinho |
| Minérios e gemas | Flint, Amber, Opal, Ruby, Sapphire, Topaz, Diamond | Rupees, upgrade de armadura, acessórios Gerudo | Flint + madeira = fogueira |
| Madeira | Wood | Fogueira (madeira + flint ou fogo) | Construir barco não existe; apenas flutuar em jangada pronta |
| Partes de dragão | Scale, Claw, Fang, Horn (Dinraal, Naydra, Farosh) | Upgrade de armadura de topo | Comida com +30:00 de duração |
| Fragmento de estrela | Star Fragment | Upgrade de acessórios e armaduras amiibo | Comida crítica garantida |

**Regras de culinária** ([Zelda Dungeon](https://www.zeldadungeon.net/wiki/Breath_of_the_Wild_Recipes), [ZeldaMods](https://zeldamods.org/wiki/Cooking))

1. Até 5 ingredientes por prato numa panela acesa. Fogueira sem panela só assa 1 item por vez.
2. Corações restaurados = soma do valor de cada ingrediente, dobrado por estar cozido (uma maçã crua = 1/2 coração; cozida em prato = 1).
3. Cada ingrediente com efeito tem uma pontuação; a soma define o nível do efeito (1, 2 ou 3). Cinco iguais dão o efeito máximo.
4. Um prato tem só UM efeito. Dois tipos de efeito no mesmo prato se anulam (o prato sai sem efeito).
5. Duração: base por efeito + acréscimo por ingrediente; máximo 30:00. Partes de dragão adicionam tempo garantido.
6. Acerto crítico aleatório (+corações, +5:00 ou +1 nível); garantido na janela da Lua de Sangue (23:30 a 00:15).
7. Elixir = ao menos 1 critter + ao menos 1 parte de monstro, sem comida. Comida + parte de monstro = Dubious Food (1 coração). Minério ou madeira = Rock-Hard Food (1/4).
8. Monster Extract randomiza duração (1:00, 10:00 ou 30:00), nível ou corações, e cancela o crítico.

**Efeitos (um por prato)**

| Efeito | Nome no jogo | O que faz | Ingrediente-chave |
| --- | --- | --- | --- |
| Hearty | Corações extras amarelos | Cura total + corações temporários além do máximo | Hearty Durian (4 cada), Hearty Truffle, Radish |
| Energizing | Restaura stamina | Recarrega a roda | Stamella Shroom, Courser Bee Honey, Energetic Rhino Beetle |
| Enduring | Stamina extra amarela | Roda temporária extra | Endura Carrot, Endura Shroom, Tireless Frog |
| Spicy | Resistência ao frio (nível 1-2) | Anula frio nível 1 ou 2 | Spicy Pepper, Warm Safflina, Sunshroom |
| Chilly | Resistência ao calor (1-2) | Anula calor nível 1 ou 2 | Hydromelon, Cool Safflina, Chillshroom |
| Electro | Resistência a choque (1-3) | Reduz dano elétrico | Voltfruit, Zapshroom, Electric Safflina |
| Fireproof | Só elixir | Imune a pegar fogo | Fireproof Lizard, Smotherwing Butterfly |
| Hasty | Velocidade (1-3) | Corrida, nado e escalada mais rápidos | Rushroom, Fleet-Lotus Seeds, Swift Carrot |
| Sneaky | Furtividade (1-3) | Reduz ruído | Silent Shroom, Blue Nightshade, Silent Princess |
| Mighty | Ataque (1-3) | Mais dano | Mighty Bananas, Razorshroom, Mighty Thistle |
| Tough | Defesa (1-3) | Menos dano | Fortified Pumpkin, Ironshroom, Armoranth |

**Economia**

- Moeda única: Rupee (verde 1, azul 5, vermelho 20, roxo 50, prata 100, ouro 300). Carteira sem limite prático.
- Fontes: vender materiais e gemas (Diamond 500, Ruby 210, Sapphire 260, Topaz 180, Amber 30), minigames, Blupees, baús, missões.
- Sumidouros: armaduras (de 50 a 4000+), Great Fairy Fountains (100, 500, 1000, 10000), casa em Hateno (3000 + 100 por móvel), registro de cavalo (20), Tarrey Town, flechas (20 por 5, elementais mais caras), estábulo (20 por noite com cama macia).
- Mon (moeda de Kilton): partes de monstro trocadas por máscaras e Dark Link; só à noite.
- Armas NUNCA são vendidas em loja (exceto flechas e alguns itens de Gerudo/Tarrey); armas vêm do mundo.

**Regra de design:** todo material deve ter ao menos dois usos (comida + venda, elixir + upgrade). Um item sem uso além de "vender" é proibido. Nenhuma cura pode ser comprada diretamente.

## Armaduras, acessórios e progressão

A progressão é horizontal: o jogador não sobe de nível, ele acumula opções (corações, stamina, slots, roupas, runas+, habilidades de Campeões), e cada uma abre um lugar ou um jeito de fazer algo.

**Fontes de progressão permanente**

| Fonte | O que dá | Quantidade | Fonte |
| --- | --- | --- | --- |
| Santuário | 1 Spirit Orb | 120 orbs = 30 trocas (4 orbs cada) | [Zelda Wiki](https://zeldawiki.wiki/wiki/Heart_Container) |
| Divine Beast | 1 Heart Container + 1 habilidade de Campeão | 4 | [ZeldaMods](https://zeldamods.org/wiki/Player/Champion_abilities) |
| Estátua da Deusa | Troca 4 orbs por Heart Container ou Stamina Vessel | Até 23 containers extras (30 corações máx.) ou 10 vessels (3 rodas) — não ambos | [Zelda Wiki](https://zeldawiki.wiki/wiki/Heart_Container) |
| Korok | Slots de inventário com Hestu | 441 sementes para o máximo | [Zelda Wiki](https://zeldawiki.wiki/wiki/Korok_Seed) |
| Great Fairy | Níveis de upgrade de armadura (1 a 4 estrelas) | 4 fadas; cada fada desbloqueada libera mais um nível | [Zelda Wiki](https://zeldawiki.wiki/wiki/Armor_Upgrade) |
| Hateno Tech Lab | Bomb+, Stasis+, Sensor+ | 3 | [Zelda Wiki](https://zeldawiki.wiki/wiki/Rune) |
| Master Sword | Requer 13 corações permanentes | 1 | Korok Forest |

**Habilidades de Campeão** (recarga em minutos reais; o DLC Champions' Ballad reduz para 1/3)

| Habilidade | Efeito | Usos / recarga |
| --- | --- | --- |
| Revali's Gale | Corrente ascendente de \~40 m a partir de qualquer ponto | 3 usos / 6 min |
| Daruk's Protection | Escudo que anula um golpe e reflete | 3 usos / 18 min |
| Mipha's Grace | Ressurreição com vida cheia + 5 corações amarelos | 1 uso / 24 min |
| Urbosa's Fury | Raio em área que paralisa e causa dano massivo | 3 usos / 12 min |

Valores de recarga são os amplamente documentados pela comunidade; o parâmetro oficial fica em GeneralParamList ([ZeldaMods](https://zeldamods.org/wiki/Player/Champion_abilities)).

**Armaduras** ([RPG Site](https://www.rpgsite.net/feature/5364-zelda-breath-of-the-wild-armor-set-outfit-locations-stats-and-bonuses), [Zelda Wiki](https://zeldawiki.wiki/wiki/Armor_Set))

- Três peças (cabeça, corpo, pernas). Defesa por peça: 1 a 5 na base, sobe com upgrade; todas as peças de mesma defesa base têm a mesma curva.
- Upgrade em Great Fairy custa materiais (não rupees); nível máximo = número de fadas desbloqueadas (100, 500, 1000, 10000 rupees para abrir).
- Bônus de set só ativa com as 3 peças em nível 2 ou mais.
- Cada set resolve UM problema do mundo (frio, calor, fogo, raio, nado, escalada, furtividade, Guardians) e tem UM bônus de set que aprofunda o mesmo problema.

| Set | Efeito | Bônus de set (3 peças, nível 2+) | Onde |
| --- | --- | --- | --- |
| Hylian | Nenhum (defesa barata) | Nenhum | Hateno |
| Soldier's | Defesa alta | Nenhum | Hateno |
| Sheikah Stealth | Furtividade | Velocidade noturna | Kakariko |
| Snowquill | Frio | Não congela | Rito Village |
| Zora | Nado rápido, sobe cachoeira | Menos stamina no dash aquático | Zora's Domain |
| Flamebreaker | Calor escaldante | Fireproof | Goron City |
| Desert Voe | Calor | Resistência a choque | Gerudo Secret Club |
| Gerudo Vai | Calor (sem upgrade) | — | Gerudo Town (quest) |
| Ancient | Resistência a Guardians | Armas ancient +dano/durabilidade | Akkala Tech Lab |
| Climber's | Escalada rápida | Menos stamina escalando | 3 santuários |
| Barbarian | Ataque | Ataque carregado gasta menos stamina | 3 santuários (labirintos) |
| Rubber | Choque | Imune a choque | Quest + 2 santuários |
| Radiant | Atrai Stal | Bônus de dano ósseo | Gerudo Secret Club |
| Dark Link | Velocidade noturna | — | Kilton (Mon) |
| Champion's Tunic | Mostra HP de inimigos | — | Impa (memórias) |

**Acessórios (peças avulsas de cabeça)**: Circlets e Earrings Gerudo (Ruby frio, Sapphire calor, Topaz choque, Diamond Guardian, Amber defesa, Opal nado) dão um efeito sem set; máscaras de Kilton (Bokoblin, Moblin, Lizalfos, Lynel) disfarçam Link da espécie; Sand Boots e Snow Boots anulam terreno lento; Korok Mask (DLC) treme perto de Korok.

**Regra de design:** nenhum item dá "+X% de dano" genérico. Todo equipamento resolve uma pressão específica do mundo e substitui (não soma) o que o jogador poderia fazer com comida ou comportamento. Equipamento permanente custa exploração (quest, santuário, loja de vila), nunca grind.

## Personagens, raças, NPCs e narrativa não linear

A história já aconteceu: o jogador acorda 100 anos depois da derrota e reconstrói o passado em qualquer ordem, enquanto o presente (Ganon preso no castelo) espera por ele sem relógio.

**Protagonista.** Link é mudo, sem memória e sem classe; a personalidade vem das ações do jogador. Tudo que ele veste, come e usa é visível no modelo (roupa, arma nas costas, escudo). Não há diálogo com escolhas morais; há escolhas de ação.

**Raças (cada uma = região + arquitetura + música + problema de sobrevivência + Divine Beast)**

| Raça | Região / vila | Identidade visual e cultural | Pressão do bioma | Campeão e Divine Beast | Habilidade dada |
| --- | --- | --- | --- | --- | --- |
| Hylian | Hateno, Kakariko (Sheikah), Lurelin, estábulos | Vila rural, telhados de palha, fazendas | Nenhuma (zona de aprendizado) | — | — |
| Sheikah | Kakariko, Tech Labs (Purah, Robbie) | Estilo japonês Jōmon, olho vermelho, tecnologia ancestral | — | — | Runas, Sensor+, Ancient gear |
| Zora | Zora's Domain (Lanayru) | Arquitetura de prata e água luminosa, aquáticos, longêvos | Chuva eterna, raios | Mipha / Vah Ruta (elefante) | Mipha's Grace |
| Goron | Goron City (Eldin) | Rocha, lava, comem pedra, fortes e amigáveis | Calor escaldante | Daruk / Vah Rudania (lagarto) | Daruk's Protection |
| Rito | Rito Village (Tabantha/Hebra) | Aves em torre de madeira, orgulhosos, músicos (Kass) | Frio e vento | Revali / Vah Medoh (águia) | Revali's Gale |
| Gerudo | Gerudo Town (Wasteland) | Cidade só de mulheres, deserto, joias | Calor de dia, frio de noite, areia movediça | Urbosa / Vah Naboris (camelo) | Urbosa's Fury |
| Korok | Korok Forest (Woodland) | Espíritos da floresta com máscara de folha, invisíveis a quem não acredita | Lost Woods (neblina) | — | Hestu (inventário), Master Sword |
| Yiga | Karusa Valley (esconderijo) | Sheikah renegados, símbolo invertido, bananas | — | Master Kohga (chefe) | Emboscadas em todo o mapa |

**NPCs-chave e função de sistema**

| NPC | Função de design |
| --- | --- |
| Zelda (voz na slate) | Objetivo e missão principal; segurando Ganon no castelo |
| King Rhoam (Velho do Platô) | Tutorial sem tutorial: ensina cozinhar, planar, sobreviver ao frio |
| Impa | Entrega a quest das memórias e o Champion's Tunic |
| Purah / Robbie | Upgrades de runas (Hateno) e armas Ancient (Akkala) |
| Hestu | Inventário por sementes Korok; aparece em 3 lugares |
| Kass | Bardo Rito; cada canção é um enigma que aponta um santuário escondido |
| Beedle | Mercador itinerante nos estábulos (flechas, materiais) |
| Kilton | Loja noturna de máscaras por partes de monstro |
| Sidon, Yunobo, Teba, Riju | Representantes da geração atual de cada raça; acompanham o assalto à Divine Beast |
| Grandes Fadas (Cotera, Kaysa, Mija, Tera) | Upgrade de armadura |
| Malanya | Deus dos cavalos; ressuscita montarias |
| Pikango, Bolson, Hudson, Tarrey Town | Side quests de construção e comunidade |

**Estrutura narrativa**

1. Grande Platô (obrigatório, \~2 h): 4 santuários = 4 runas; paraglider libera o mundo.
2. Quatro arcos regionais em qualquer ordem: chegar à vila, ganhar confiança, ganhar um item de acesso (Zora Armor, Flamebreaker, Revali's Gale prévio, Thunder Helm), invadir a Divine Beast, vencer o Blight, ganhar a habilidade do Campeão.
3. Memórias: 12 fotos no álbum da slate + 1 revelada por Impa = 13 cenas do passado, encontradas pelo cenário na foto (o jogador compara a imagem com o mundo real). 5 extras no DLC ([Nintendo Life](https://www.nintendolife.com/guides/zelda-breath-of-the-wild-all-captured-memories-locations)).
4. Castelo de Hyrule e Ganon: acessível desde o minuto 1 após o Platô; cada Blight vencido remove 1/4 do HP do chefe final; o final verdadeiro exige todas as memórias.

**Regras de narrativa e NPC**

- Toda informação de história é opcional e descoberta pelo mundo (ruínas, diários, memórias, Kass). Nunca cutscene obrigatória no meio da exploração.
- NPCs têm rotina de 24 h (trabalham, dormem, se abrigam da chuva) e reagem ao que Link veste (Gerudo Vai entra na cidade, máscara de Bokoblin engana Bokoblins, roupa Gerudo masculina é barrada).
- Viajantes nas estradas são fonte de rumor (apontam lugares) e de emboscada Yiga.
- Side quests (76 no total) são curtas, locais, recompensam com rupees, materiais ou acesso, nunca com poder permanente obrigatório.
- Vilões não falam: Ganon é força da natureza, não personagem. Ameaça visual (névoa roxa, Guardians) substitui discurso.

## Santuários, Divine Beasts, puzzles e design de dungeons

As masmorras são curtas e muitas (120 santuários de 3 a 10 min) em vez de longas e poucas; as quatro grandes (Divine Beasts) são máquinas móveis que o jogador manipula como um único puzzle de corpo inteiro.

**Santuários (Ancient Shrines)**

| Tipo | Proporção aproximada | Conteúdo | Exemplo |
| --- | --- | --- | --- |
| Puzzle de runa | \~50 % | Um conceito físico explorado em 2 a 4 salas: Magnesis, Stasis, Cryonis, bombas, correntes elétricas, vento, bolas rolantes | Oman Au, Ja Baij, Keh Namut, Owa Daim |
| Teste de força | \~20 % | Combate contra Guardian Scout (Minor II, Modest III, Major IV) numa arena | Ta'loh Naeg ensina esquiva e parry |
| Bênção | \~25 % | Sem puzzle interno; o desafio foi chegar (Shrine Quest, canção de Kass, labirinto) | Labirintos de Lomei, Eventide Island |
| Controle de movimento | \~5 % | Inclinar labirinto físico | Myahm Agana |

Regras: todo santuário tem 1 Spirit Orb, 1 a 3 baús opcionais, entrada visual única (brilho laranja), interior de mesmo material (pedra azul Sheikah, não escalável) e aceita soluções não previstas (Stasis em bola, bomba para atravessar). Duração alvo: 3 a 10 minutos. O tipo de desafio é anunciado pelo nome do monge ("Modest Test of Strength").

**Shrine Quests:** 42 santuários estão escondidos e exigem resolver o mundo primeiro (acender tochas na ordem certa, levar uma bola de pedra até um buraco, chegar à ilha sem equipamento, seguir uma canção de Kass). O puzzle externo vale mais que o interno.

**Divine Beasts** ([Zelda Universe](https://zeldauniverse.net/2017/06/06/fujibayashi-the-concept-behind-breath-of-the-wilds-divine-beasts-why-ganon-has-no-voice/))

| Beast | Forma | Mecânica de corpo (controlada pelo mapa da slate) | Acesso | Chefe |
| --- | --- | --- | --- | --- |
| Vah Ruta | Elefante | Tromba sobe e desce, despejando água em rodas e engrenagens | Subir a cachoeira com Sidon, flechas de choque nos reservatórios | Waterblight Ganon |
| Vah Rudania | Lagarto | Corpo inclina 90°, mudando qual parede é chão | Escoltar Yunobo em canhões pela montanha | Fireblight Ganon |
| Vah Medoh | Águia | Inclina para os lados, movendo bolas e plataformas | Voo com Teba, flechas nos canhões | Windblight Ganon |
| Vah Naboris | Camelo | Três seções do corpo giram independentes, alinhando circuitos elétricos | Perseguir com seal, bombas nos pés | Thunderblight Ganon |

Estrutura comum: 1 sequência de aproximação externa com aliado da raça, 1 interior aberto sem portas trancadas, 5 terminais ativáveis em qualquer ordem, 1 terminal principal que invoca o Blight, 1 Heart Container + 1 habilidade de Campeão + 1 memória do Campeão. Sem chaves, sem itens de masmorra: as 4 runas resolvem tudo. Duração alvo: 45 a 90 min.

**Castelo de Hyrule:** masmorra aberta a céu aberto, sem terminais; várias entradas (portão, docas, escalar), armas reais nas salas, Guardians por toda parte, biblioteca com diários (história opcional), Lynel no santuário interno. Chefe final em duas fases: Calamity Ganon (arena, 8000 HP, menos 1/4 por Blight vencido) e Dark Beast Ganon (campo aberto, Bow of Light, a cavalo).

**Regras de puzzle (toda sala nova):**

1. A sala demonstra um conceito físico em 3 passos: mostrar (seguro), aplicar (um objeto), combinar (dois objetos ou um inimigo).
2. Toda solução alternativa que respeite a física é solução válida; o jogo nunca cancela uma "trapaça" física.
3. O baú opcional exige aplicar o conceito num grau a mais.
4. Sem texto de dica dentro do santuário; a geometria é a dica.
5. Falhar devolve o jogador ao início da sala, nunca do santuário (exceto queda em abismo: perde 1/4 de coração e volta à borda).

**Regra de design:** uma masmorra nova só é aprovada se o jogador puder entrar nela em qualquer ponto da campanha com apenas as 4 runas e o paraglider.

## Direção de arte, áudio e interface

A arte serve à legibilidade: cores saturadas e formas simples deixam o jogador ler à distância o que é escalável, perigoso, metálico ou coletável, e a UI só mostra o que a cena não consegue mostrar.

**Estilo visual** ([Zelda Universe](https://zeldauniverse.net/2017/03/14/takizawa-explains-how-the-wind-waker-hd-inspired-the-visual-style-for-breath-of-the-wild/), [Nintendo Everything](https://nintendoeverything.com/art-director-on-why-zeldas-art-style-constantly-changes-and-how-breath-of-the-wilds-look-was-made/))

- Pintura (painterly) entre o cel-shading de Wind Waker e o realismo de Twilight Princess: texturas pintadas com pincelada visível, sombras suaves, contorno fino; Satoru Takizawa cita Wind Waker HD como ponto de partida e Eiji Aonuma descreve o estilo como guache ao ar livre (en plein air).
- Paleta por região (prints 6, 7, 9): Akkala vermelho-outono, Hebra branco-azul, Eldin preto-laranja, Gerudo ocre, Faron verde-tropical, Lost Woods verde-neblina, Central verde-pastagem com castelo cinza.
- Luz volumétrica e neblina de distância em camadas (print 2) para separar planos e indicar profundidade de viagem.
- Tecnologia Sheikah em azul-ciano brilhante (ativo) e laranja (inativo/corrompido), inspirada no período Jōmon; corrupção de Ganon em roxo-magenta pulsante com olhos.
- Silhueta primeiro: todo inimigo, NPC e item precisa ser identificável em sombra pura a 50 m.
- Código de cor funcional: metal = brilho e reflexo (Magnesis); madeira = marrom opaco (queima); explosivo = barril vermelho; coletável = brilho sutil + nome ao aproximar; escalável = toda rocha com textura não lisa.

**Áudio** ([Classic FM](https://www.classicfm.com/music-news/new-zelda-breath-of-the-wild-theme-tune-composers/), [Destructoid](https://www.destructoid.com/?p=264144))

- Música de campo: piano esparso, frases curtas separadas por silêncio, sem loop perceptível (Manaka Kataoka: a música não deve impor um sentimento; o silêncio deixa ouvir o ambiente).
- Música temática só em lugares: vilas (tema por raça, versão dia/noite), estábulos, santuários (tema eletrônico de Yasuaki Iwata, sem piano), Divine Beasts (orquestra), Castelo (tema clássico de Zelda, a única citação direta da série).
- Combate: stinger ao ser detectado; tema de Guardian (piano frenético) e de Lynel são avisos de perigo que começam antes de ver o inimigo.
- Som diegese: passos por material, vento por altitude, chuva, grilo à noite, corneta de Bokoblin, apito do Sensor, carrilhão do Korok. O som substitui marcadores de mapa.
- Vozes só em cutscenes de memória e Campeões; NPCs falam com texto e "grunhidos".

**HUD (visível em todas as prints)** ([The Famicast](https://www.thefamicast.com/2017/12/part-2-immersive-impressionable-ui.html), [Zelda Universe](https://zeldauniverse.net/2017/02/08/breath-of-the-wild-will-offer-a-trimmed-down-pro-hud-option/))

| Elemento | Posição | Regra |
| --- | --- | --- |
| Corações | Superior esquerdo | 1 coração = 4 quartos; amarelos para temporários; linha quebra a cada 10 |
| Roda de stamina | Ao lado de Link, só enquanto gasta | Verde; amarela para extra; pisca vermelha ao esgotar |
| Runa ativa, arma, escudo, arco | Abaixo dos corações, ícones pequenos | Troca rápida com d-pad segurando (menu radial que pausa o tempo) |
| Minimapa circular | Inferior direito | Norte fixo, mostra pins, beacons, santuários e torres descobertos |
| Relógio, termômetro, medidor de ruído, previsão de clima | Ao lado do minimapa | Termômetro muda de cor em zona de perigo; ruído cresce ao correr |
| Prompts de botão | Centro inferior | Só aparecem com ação contextual ("Jump X", "Let go B", "Soothe L") |
| Seta de lock-on | Sobre o inimigo | Laranja; mostra HP só com Champion's Tunic |
| Nome do item | Centro, ao aproximar | Sem ícone, só texto; some ao afastar |
| Pro HUD (opção) | — | Remove tudo exceto corações; o jogo precisa continuar jogável assim |

**Menus:** o inventário é a Sheikah Slate (abas: armas, arcos, escudos, armaduras, materiais, comida, itens-chave; mapa; compendium; sistema). Abrir o menu pausa o mundo. Comer e trocar roupa são feitos no menu, sem animação longa. O mapa é topográfico, sem ícones de quest não descobertos.

**Regra de design:** se o Pro HUD (só corações) tornar uma mecânica injogável, a mecânica não está comunicando pelo mundo e precisa ser refeita.

## Templates obrigatórios

Todo elemento novo nasce preenchendo um destes templates; um campo em branco significa que o elemento não está pronto. Os campos "Multiplica com" e "Matriz química" nunca podem ficar vazios.

**Template A: Mecânica ou sistema**

```markdown
# [Nome da mecânica]
- Verbo do jogador: (o que o jogador faz, em uma palavra)
- Pilar atendido: multiplicativo / físico-químico / exploração (marque ao menos 2)
- Recurso que consome: stamina / durabilidade / material / tempo / nenhum
- Entrada: botão e contexto em que o prompt aparece
- Regra física: massa, velocidade, alcance, duração, recarga (números)
- Multiplica com: lista de pelo menos 3 sistemas existentes e o resultado de cada combinação
- Vale para inimigos também? sim (obrigatório) / justificar exceção
- Como o mundo ensina (sem texto): cena ou objeto que demonstra
- Leitura no Pro HUD: como o jogador percebe o estado só pelo mundo
- Exceções e limites: onde não funciona e por quê (santuário, chuva, água)
```

**Template B: Inimigo ou criatura**

```markdown
# [Nome]
- Família e tier: (vermelho/azul/preto/prata ou único)
- HP e dano base: (seguir a curva ~3x por tier)
- Silhueta e leitura a 50 m: forma, cor, som, luz
- Habitat e horário: regiões, dia/noite, clima
- Rotina quando não vê o jogador: 3 atividades
- Percepção: cone de visão, audição, reação a disfarce
- Arma ou ataque: usa armas do chão? elemento?
- Matriz química: reação a fogo, gelo, eletricidade, vento, água
- Soluções sem arma: pelo menos 2 (terreno, física, furtividade, fuga)
- Drops: partes fixas + arma portada
- Pontos de escala: quanto conta para o World Level
```

**Template C: Item, arma, material ou consumível**

```markdown
# [Nome]
- Categoria: arma 1 mão / 2 mãos / lança / arco / escudo / material / comida / elixir
- Material físico: madeira / metal / pedra / osso / ancient / orgânico
- Números: ataque, durabilidade (ou corações, efeito, nível, duração)
- Onde aparece no mundo: inimigo que porta, região, baú, loja (armas nunca em loja)
- Dois usos mínimos: (ex.: comida + venda; elixir + upgrade)
- Matriz química: o que acontece com fogo, raio, água, gelo
- Modificadores possíveis: Attack Up, Durability Up, etc. ou nenhum
- Como o jogador descobre: visto em inimigo / NPC menciona / compendium
- Curva: em qual tier de inimigo começa a aparecer
```

**Template D: Armadura ou acessório**

```markdown
# [Nome]
- Peças: cabeça / corpo / pernas / avulso
- Defesa base e curva de upgrade (materiais por nível, 4 níveis)
- Pressão do mundo que resolve: frio / calor / fogo / raio / nado / escalada / furtividade / Guardians / social (disfarce)
- Alternativa por comida e por comportamento: (obrigatório listar as duas)
- Bônus de set (nível 2+): aprofunda a mesma pressão
- Como NPCs e inimigos reagem a ela
- Onde e como se obtém: vila, quest, santuário (nunca drop aleatório)
- Aparência: cultura/raça de origem, silhueta reconhecível
```

**Template E: Personagem ou NPC**

```markdown
# [Nome]
- Raça e vila
- Função de sistema: loja / upgrade / quest / rumor / tutorial implícito / companheiro de Divine Beast
- Rotina de 24 h: onde está a cada período, onde se abriga da chuva
- Reações a Link: roupa, máscara, hora, estado (a cavalo, molhado, pegando fogo)
- O que ele sabe do mundo: 1 a 3 pistas que apontam lugares visíveis
- Quest que oferece: objetivo local, recompensa (rupees, material, acesso), duração alvo
- Voz: texto apenas; não fala em cutscene salvo Campeões e Zelda
```

**Template F: Região, local ou santuário**

```markdown
# [Nome]
- Escala: região / vila / acampamento / santuário / ponto de interesse
- Triângulo: o que esconde e o que revela ao ser contornado ou escalado
- Visível de onde: lista de 3 mirantes de onde o jogador o vê
- Pressão de sobrevivência: clima/temperatura e as 3 contramedidas
- Conteúdo mínimo: torre, santuários, estábulo, acampamentos, Koroks, elite
- Raça residente e paleta de cor
- Como se chega em qualquer ordem: pelo menos 2 rotas (uma a pé, uma pelo ar)
- Música e som característico
- Para santuário: conceito físico, 3 passos (mostrar, aplicar, combinar), baú opcional, duração alvo
```

## Checklist de validação e lista de proibições

Um elemento só é aprovado com todos os itens do checklist marcados e nenhuma proibição violada.

**Checklist (marcar antes de aprovar)**

- [ ] Atende a pelo menos 2 dos 3 pilares (multiplicativo, físico-químico, exploração)
- [ ] Interage com 3 ou mais sistemas existentes, com cada combinação descrita
- [ ] Linha preenchida na matriz elemento × material
- [ ] Vale igualmente para inimigos e NPCs (ou exceção justificada)
- [ ] Pode ser encontrado ou usado em qualquer ordem da campanha
- [ ] É legível à distância por silhueta, cor, luz ou som
- [ ] Jogável com Pro HUD (só corações)
- [ ] Ensinado pelo mundo, sem caixa de texto
- [ ] Consome ou disputa um recurso finito (stamina, durabilidade, material, tempo)
- [ ] Tem ao menos uma solução ou uso alternativo sem combate
- [ ] Números definidos (HP, dano, duração, custo) e coerentes com as curvas deste documento
- [ ] Tem raça, região ou cultura de origem na arte
- [ ] Template correspondente (A a F) totalmente preenchido

**Proibições (rejeição automática)**

| Proibido | Por que fere o padrão | O que fazer no lugar |
| --- | --- | --- |
| Nível de personagem, XP, árvore de talentos, classes | Progressão vertical substitui exploração por grind | Corações, stamina, slots, equipamento com função |
| Raridade por cor de loot, "+5 % de dano" genérico | Poder abstrato não muda como se joga | Modificadores concretos (Durability Up, Quick Shot) ou efeito que resolve uma pressão do mundo |
| Arma indestrutível (além da Master Sword) | Elimina a economia de combate | Recarga, como a Master Sword |
| Cura comprável, poção de loja, regeneração automática | Remove a razão de colher e cozinhar | Comida e elixir |
| Muro invisível, porta de história, área bloqueada por nível | Quebra a liberdade | Fronteira natural, pressão de sobrevivência, inimigo forte |
| Marcador de quest para lugar não visto | Tira o olhar como guia | NPC descreve, jogador marca com a luneta |
| Puzzle com solução única forçada ou que cancela física | Contradiz gameplay multiplicativo | Aceitar qualquer solução física |
| Regra que vale só para o jogador (inimigo imune a fogo sem motivo de material) | Quebra a consistência do motor químico | Preencher a matriz igual para todos |
| Cutscene obrigatória no meio do campo, tutorial em texto | Interrompe a exploração | Cena ambiental, NPC opcional |
| Habilidade nova no meio do jogo que abre áreas | Volta ao modelo de item-chave | Todas as ferramentas no início; áreas abertas por conhecimento e preparação |
| Minimapa com inimigos, itens ou segredos não descobertos | Tira a descoberta | Mapa topográfico + pins do jogador |
| Música contínua em campo, vozes em diálogo comum | Rompe o silêncio que guia pelo som | Piano esparso, som diegese |
| Inimigo sem rotina, sem percepção, sem reação química | É um saco de HP, não um habitante | Template B completo |
| Item com um só uso ("vendível") | Material sem papel no mundo | Dois usos mínimos |
| Fast travel para lugar não visitado | Pula a viagem, que é o jogo | Só torres e santuários ativados |

**Critério final:** se um jogador que só jogou Breath of the Wild pegar o elemento novo, ele deve saber usar sem explicação e deve descobrir pelo menos uma combinação que não foi planejada.

## Fontes consultadas

Páginas abertas e usadas nos números e citações deste documento. Valores de HP vêm do datamine; valores sem fonte ligada (recarga das habilidades de Campeão, duração de Stasis, alcance de Magnesis) são os documentados pela comunidade e devem ser lidos como aproximados.

| Tema | Fonte |
| --- | --- |
| Filosofia de design, GDC 2017 (Fujibayashi, Dohta, Takizawa) | [Tom's Guide](https://www.tomsguide.com/uk/us/zelda-breath-wild-design,news-24586.html), [Thumbsticks (motor de química)](https://www.thumbsticks.com/gdc-17-breath-of-the-wild-science-lies), [Thumbsticks (design)](https://www.thumbsticks.com/gdc17-designing-zelda-breath-of-the-wild/) |
| Regra do triângulo, gravidade do mapa | [Source Gaming](https://sourcegaming.info/2017/11/25/holism-breath-of-the-wilds-golden-triangles/), [80.lv](https://80.lv/articles/the-design-secrets-of-breath-of-the-wild/?amp=1), [GMTK](https://gmtk.substack.com/p/how-nintendo-solved-zeldas-open-world) |
| HP de inimigos (datamine) | [GitHub gist m-byte918](https://gist.github.com/m-byte918/1d1acc569ad1cd59ead8e151fddf4591), [GameFAQs Enemy Statistics](https://gamefaqs.gamespot.com/switch/189707-the-legend-of-zelda-breath-of-the-wild/faqs/75488/enemy-statistics) |
| Escala de dificuldade, modificadores de arma, HP dos Blights | [ZeldaMods: Difficulty scaling](https://zeldamods.org/wiki/Difficulty_scaling) |
| Lua de Sangue | [ZeldaMods: Blood moon](https://zeldamods.org/wiki/Blood_moon) |
| Culinária (motor, crítico) | [ZeldaMods: Cooking](https://zeldamods.org/wiki/Cooking), [Zelda Dungeon: Recipes](https://www.zeldadungeon.net/wiki/Breath_of_the_Wild_Recipes), [Zelda Wiki: Ingredients](https://www.zeldawiki.wiki/wiki/Ingredients) |
| Habilidades de Campeão (parâmetros) | [ZeldaMods: Champion abilities](https://zeldamods.org/wiki/Player/Champion_abilities) |
| Durabilidade e combate | [Zelda Wiki: Durability](https://zeldawiki.wiki/wiki/Durability), [RPG Site](https://rpgsite.net/feature/5365-the-legend-of-zelda-breath-of-the-wild-guide-tips-on-weapons-durability-and-combat), [Zelda Wiki: Perfect Dodging](https://zelda.fandom.com/wiki/Perfect_Dodging), [Gamer Guides: armas](https://www.gamerguides.com/the-legend-of-zelda-breath-of-the-wild/guide/weapons-and-armor) |
| Stamina, corações, Estátua Chifruda | [Zelda Wiki: Stamina Wheel](https://zeldawiki.wiki/wiki/Stamina_Wheel), [Zelda Wiki: Heart Container](https://zeldawiki.wiki/wiki/Heart_Container), [Zelda Wiki: Stamina Vessel](https://zeldawiki.wiki/wiki/Stamina_Vessel) |
| Sementes Korok e inventário | [Zelda Wiki: Korok Seed](https://zeldawiki.wiki/wiki/Korok_Seed), [Zelda Dungeon: Korok Seed](https://www.zeldadungeon.net/wiki/Korok_Seed), [Prima Games](https://primagames.com/tips/legend-zelda-breath-wild-increase-inventory-size-priceless-maracas) |
| Runas | [Zelda Dungeon: Remote Bomb](https://www.zeldadungeon.net/wiki/Remote_Bomb), [Zelda Wiki: Rune](https://zeldawiki.wiki/wiki/Rune), [GameWith: Stasis](https://gamewith.net/zelda-botw/article/show/36040) |
| Temperatura e clima | [Zelda Wiki: Temperature](https://zeldawiki.wiki/wiki/Temperature), [GameWith](https://gamewith.net/zelda-botw/article/show/38815) |
| Cavalos | [Zelda Dungeon: Horse](<https://www.zeldadungeon.net/wiki/Horse_(Breath_of_the_Wild)>) |
| Armaduras e Great Fairies | [RPG Site: Armor Sets](https://www.rpgsite.net/feature/5364-zelda-breath-of-the-wild-armor-set-outfit-locations-stats-and-bonuses), [Zelda Wiki: Armor Upgrade](https://zeldawiki.wiki/wiki/Armor_Upgrade), [Zelda Wiki: Armor Set](https://zeldawiki.wiki/wiki/Armor_Set) |
| Regiões, torres, santuários | [Zelda Dungeon: Shrines](https://www.zeldadungeon.net/?p=91923), [Nintendo Life: Shrines](https://www.nintendolife.com/guides/zelda-breath-of-the-wild-all-shrine-locations-and-maps), [Prima: Shrines](https://primagames.com/tips/legend-zelda-breath-wild-shrines-guide-and-locations) |
| Memórias e narrativa | [Nintendo Life: Memories](https://www.nintendolife.com/guides/zelda-breath-of-the-wild-all-captured-memories-locations), [Zelda Universe: Divine Beasts e Ganon](https://zeldauniverse.net/2017/06/06/fujibayashi-the-concept-behind-breath-of-the-wilds-divine-beasts-why-ganon-has-no-voice/) |
| Personagens | [Wikipedia: personagens](https://en.wikipedia.org/wiki/List_of_The_Legend_of_Zelda:_Breath_of_the_Wild_and_Tears_of_the_Kingdom_characters), [Zelda Dungeon: Key Characters](https://www.zeldadungeon.net/wiki/Breath_of_the_Wild_Key_Characters) |
| Arte | [Zelda Universe: Takizawa](https://zeldauniverse.net/2017/03/14/takizawa-explains-how-the-wind-waker-hd-inspired-the-visual-style-for-breath-of-the-wild/), [Nintendo Everything](https://nintendoeverything.com/art-director-on-why-zeldas-art-style-constantly-changes-and-how-breath-of-the-wilds-look-was-made/), [TweakTown](https://www.tweaktown.com/news/63954/legend-zelda-breath-wild-art-style-explained/index.html) |
| Áudio | [Classic FM](https://www.classicfm.com/music-news/new-zelda-breath-of-the-wild-theme-tune-composers/), [Destructoid](https://www.destructoid.com/?p=264144), [Zelda Dungeon: Shrine music](https://www.zeldadungeon.net/?p=143757) |
| HUD | [The Famicast](https://www.thefamicast.com/2017/12/part-2-immersive-impressionable-ui.html), [Zelda Universe: Pro HUD](https://zeldauniverse.net/2017/02/08/breath-of-the-wild-will-offer-a-trimmed-down-pro-hud-option/), [Kotaku](https://kotaku.com/if-you-re-playing-zelda-breath-of-the-wild-turn-off-t-1792734400) |
