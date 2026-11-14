<p align="center">
  <img src="docs/icone.png" width="96" alt="Ícone do OLED Descanso">
</p>

<h1 align="center">OLED Descanso</h1>

<p align="center">
  Apaga o seu monitor OLED quando ele fica sem uso, <b>mesmo que você continue usando o outro monitor</b>.<br>
  Leve, simples e sem instalação, para Windows 10 e 11.
</p>

---

## Por que existe

O Windows só apaga as telas quando **o PC inteiro** fica sem uso. Quem tem dois monitores e usa o segundo o dia todo acaba com o OLED **sempre aceso**, com barra de tarefas, ícones e janelas paradas, que é o que causa marcas (burn-in).

O OLED Descanso cuida de **cada monitor separadamente**: se você não está usando o OLED, ele apaga o OLED, e o resto continua normal.

No OLED, preto é **pixel desligado**, então a tela preta do programa equivale a desligar o painel, sem nenhum desgaste.

## Recursos

- **Apaga por monitor**, cada um com seu próprio tempo (15 s a 10 min).
- **Atalho de teclado** (padrão `Ctrl+Alt+B`, configurável) para apagar na hora. Aperte de novo para voltar.
- **Inteligente com vídeos e jogos:**
  - não apaga o monitor onde um vídeo está tocando;
  - não apaga enquanto você joga em tela cheia (inclusive só com controle Xbox/XInput);
  - **limite de segurança:** se ninguém mexer no PC por X horas, apaga mesmo com vídeo (útil se você dormir com uma playlist rodando).
- **Esmaecer suave** em vez de cortar de uma vez (opcional).
- **Desligar de verdade** o monitor (modo de espera via DDC/CI), por monitor (opcional).
- **Esconde o ponteiro do mouse** parado em cima do OLED (opcional).
- **Contador de proteção:** quanto tempo cada monitor ficou apagado hoje.
- **Pausar** por 15 min, 30 min, 1 h, 2 h ou até você retomar.
- Menu com **tema claro/escuro** que segue o Windows.
- Funciona com monitores em **escalas diferentes** (ex.: 100% e 175%).

## Como instalar

1. Baixe o **`OledDescanso.exe`** na página de [**Releases**](../../releases/latest).
2. Coloque o arquivo numa pasta fixa, por exemplo `C:\Users\<você>\AppData\Local\Programs\OledDescanso\`.
3. Dê dois cliques nele. O ícone 🌙 aparece perto do relógio.
4. Clique com o **botão direito** no ícone para configurar. Para abrir junto com o Windows, marque **Configurações ▸ Iniciar com o Windows**.

> **Aviso do Windows (SmartScreen):** como o programa é novo e não tem assinatura digital paga, o Windows pode mostrar *"O Windows protegeu o computador"*. Clique em **Mais informações ▸ Executar assim mesmo**.
>
> **Antivírus:** alguns antivírus desconfiam de programas pequenos e desconhecidos. O código está todo aqui no repositório para quem quiser conferir; se for bloqueado, adicione uma exceção para o `OledDescanso.exe`.

Requisitos: Windows 10 ou 11 (o .NET Framework 4.8 necessário já vem no Windows).

## Como usar

| No menu do ícone | O que faz |
|---|---|
| **Apagar agora** | Apaga na hora os monitores marcados (mesmo que o atalho) |
| **Pausar** ▸ | Pausa por um tempo ou até você retomar. Dois cliques no ícone também pausam/retomam |
| **Monitores** ▸ *monitor* | Liga/desliga o apagar daquele monitor, escolhe o tempo e o "desligar de verdade" |
| **Comportamento** ▸ | Esmaecer, regras de vídeo, limite sem uso, esconder mouse |
| **Configurações** ▸ | Trocar atalho, contador, iniciar com o Windows, atalho no menu Iniciar, tema |

Para acordar um monitor apagado: **mexa o mouse nele** ou aperte o atalho de novo.

## Privacidade

- **Não usa internet.** Não há nenhum código de rede no programa.
- Para saber **em qual monitor o vídeo está**, o programa compara "fotos" minúsculas da tela (**64 × 36 pixels**, impossível ler qualquer coisa), só quando há vídeo tocando e só nos últimos segundos antes de apagar. As fotos ficam **apenas na memória** (no máximo uma por monitor), **nunca são salvas nem enviadas**, e são descartadas em seguida. Isso pode ser desligado em **Comportamento ▸ Detectar monitor do vídeo (fotos)**.
- O atalho usa o recurso oficial de atalhos do Windows (`RegisterHotKey`). O programa **não lê o que você digita**.
- Tudo o que é gravado: o arquivo de configurações `%APPDATA%\OledDescanso.ini` e, se você ativar, a entrada de iniciar com o Windows e o atalho no menu Iniciar.

## Consumo

Medido em uso real: cerca de **24 MB de RAM** e **~0,002% de CPU** (16 ms de processamento por minuto), com prioridade baixa. Não afeta jogos.

## Compilar o código

Não precisa instalar nada: o Windows já traz o compilador C#.

```bat
build.bat
```

Gera o `OledDescanso.exe` na mesma pasta. O código inteiro está em [`OledDescanso.cs`](OledDescanso.cs).

## Desinstalar

1. No menu do ícone, desmarque **Iniciar com o Windows** e **Atalho no menu Iniciar**.
2. Clique em **Sair**.
3. Apague o `OledDescanso.exe` e o arquivo `%APPDATA%\OledDescanso.ini`.

Não fica mais nada no sistema.

## Licença

[MIT](LICENSE): use, modifique e distribua à vontade.
