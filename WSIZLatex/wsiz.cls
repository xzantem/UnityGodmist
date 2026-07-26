%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
% File:     wsiz.cls
% Author:   Ewa Żesławska
% Version:  2026/01/14 v3.1 (clean + numbering fixed)
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

\NeedsTeXFormat{LaTeX2e}
\ProvidesClass{wsiz}[2026/01/14 v3.1 WSIiZ thesis class]

\message{*** [wsiz.cls] --- (EZ) v3.1 (clean + numbering fixed) ***}

%---------------------------------------------------------------------------
% OPTIONS
%---------------------------------------------------------------------------

\newif\ifCLASSOPTIONpl     \CLASSOPTIONpltrue
\newif\ifCLASSOPTIONen     \CLASSOPTIONenfalse
\newif\ifCLASSOPTIONauxsup \CLASSOPTIONauxsupfalse
\newif\ifCLASSOPTIONsecsup \CLASSOPTIONsecsupfalse

\DeclareOption{pl}{\CLASSOPTIONpltrue \CLASSOPTIONenfalse}
\DeclareOption{en}{\CLASSOPTIONentrue \CLASSOPTIONplfalse}
\DeclareOption{auxsup}{\CLASSOPTIONauxsuptrue}
\DeclareOption{secsup}{\CLASSOPTIONsecsuptrue}
\DeclareOption*{\PassOptionsToClass{\CurrentOption}{report}}

\ProcessOptions\relax
\LoadClass[a4paper,12pt,oneside]{report}

%---------------------------------------------------------------------------
% PACKAGES (no duplicates)
%---------------------------------------------------------------------------

% Fonts: XeLaTeX/LuaLaTeX expected (fontspec)
\RequirePackage{fontspec}
\defaultfontfeatures{Ligatures=TeX}

\setmainfont{Times New Roman}
\setsansfont{Arial}
\setmonofont{Courier New}

\RequirePackage{graphicx}
\RequirePackage{geometry}
\RequirePackage[dvipsnames]{xcolor}
\RequirePackage{url}
\RequirePackage{titlesec}
\RequirePackage{tocloft}
\RequirePackage{caption}
\RequirePackage{subcaption}
\RequirePackage{siunitx}
\RequirePackage{enumitem}
\RequirePackage[all,defaultlines=2]{nowidow}
\RequirePackage{setspace}
\RequirePackage{microtype}
\RequirePackage{ragged2e}
\RequirePackage{fancyhdr}
\RequirePackage[hidelinks]{hyperref}

%---------------------------------------------------------------------------
% GEOMETRY / BASIC TYPO
%---------------------------------------------------------------------------

\geometry{lmargin=20mm, rmargin=20mm, tmargin=20mm, bmargin=20mm}
\setstretch{1.15}

\setlength{\parindent}{1.25cm}
\setlength{\parskip}{0pt}

\lefthyphenmin=2
\righthyphenmin=2

\AtBeginDocument{\justifying}

%---------------------------------------------------------------------------
% HEADINGS (report: chapter=rozdział, section=podrozdział)
%---------------------------------------------------------------------------

% Rozdział: 14 pt, bold
\titleformat{\chapter}[block]
  {\bfseries\fontsize{14}{16}\selectfont}
  {\thechapter.}
  {0.75em}{}
\titlespacing*{\chapter}{0pt}{0pt}{10pt}

% Podrozdział: 12 pt, bold
\titleformat{\section}[block]
  {\bfseries\fontsize{12}{14}\selectfont}
  {\thesection.}
  {0.75em}{}
\titlespacing*{\section}{0pt}{10pt}{6pt}

\titleformat{\subsection}[block]
  {\bfseries\fontsize{12}{14}\selectfont}
  {\thesubsection.}
  {0.75em}{}
\titlespacing*{\subsection}{0pt}{8pt}{5pt}

%---------------------------------------------------------------------------
% TOC formatting (clean, consistent)
%---------------------------------------------------------------------------

\renewcommand{\cfttoctitlefont}{\bfseries\fontsize{14}{16}\selectfont}
\renewcommand{\cftsecfont}{\fontsize{12}{14}\selectfont}
\renewcommand{\cftsubsecfont}{\fontsize{12}{14}\selectfont}
\renewcommand{\cftsecpagefont}{\fontsize{12}{14}\selectfont}
\renewcommand{\cftsubsecpagefont}{\fontsize{12}{14}\selectfont}
\renewcommand{\cftloftitlefont}{\bfseries\fontsize{14}{16}\selectfont}
\renewcommand{\cftlottitlefont}{\bfseries\fontsize{14}{16}\selectfont}
\renewcommand{\cftaftertoctitleskip}{10pt}
\renewcommand{\cftafterloftitleskip}{10pt}
\renewcommand{\cftafterlottitleskip}{10pt}



% --- Polskie nazwy spisów ---
\AtBeginDocument{%
  \renewcommand{\contentsname}{Spis treści}%
  \renewcommand{\listfigurename}{Spis rysunków}%
  \renewcommand{\listtablename}{Spis tabel}%
  \renewcommand{\figurename}{Rys.}%
  \renewcommand{\tablename}{Tab.}%
}

% Source line (10 pt, non-italic)
\newcommand{\wsizsource}[1]{%
  \par\noindent\centering{\fontsize{10}{12}\selectfont Źródło: #1}\par
}


%---------------------------------------------------------------------------
% PAGE NUMBERING (WSIiZ: footer centered, 12pt; hidden on title page)
%---------------------------------------------------------------------------

\pagestyle{fancy}
\fancyhf{}
\fancyfoot[C]{\fontsize{12}{14}\selectfont\thepage}
\renewcommand{\headrulewidth}{0pt}
\renewcommand{\footrulewidth}{0pt}

% Important: report uses 'plain' style on chapter starting pages -> override it
\fancypagestyle{plain}{%
  \fancyhf{}%
  \fancyfoot[C]{\fontsize{12}{14}\selectfont\thepage}%
  \renewcommand{\headrulewidth}{0pt}%
  \renewcommand{\footrulewidth}{0pt}%
}

% Optional extra footer text (author + short title) WITHOUT removing page number
\makeatletter
\providecommand\@shorttitlePL{}
\providecommand\@shorttitleEN{}
\providecommand\@shortauthor{}
\providecommand\@titlePL{}
\providecommand\@titleEN{}
\providecommand\@faculty{}
\providecommand\@thesistype{}
\providecommand\@supervisor{}
\providecommand\@secondsupervisor{}
\providecommand\@acknowledgements{}
\providecommand\@department{}
\providecommand\@degreeprogramme{}
\providecommand\@specialization{}
\providecommand\@noAlbum{}
\providecommand\@thesisDone{}

\def\shorttitlePL#1{\gdef\@shorttitlePL{#1}}
\def\shorttitleEN#1{\gdef\@shorttitleEN{#1}}
\def\shortauthor#1{\gdef\@shortauthor{#1}}
\def\titlePL#1{\gdef\@titlePL{#1}}
\def\titleEN#1{\gdef\@titleEN{#1}}
\def\faculty#1{\gdef\@faculty{#1}}
\def\thesistype#1{\gdef\@thesistype{#1}}
\def\supervisor#1{\gdef\@supervisor{#1}}
\def\secondsupervisor#1{\gdef\@secondsupervisor{#1}}
\def\acknowledgements#1{\gdef\@acknowledgements{#1}}
\def\department#1{\gdef\@department{#1}}
\def\degreeprogramme#1{\gdef\@degreeprogramme{#1}}
\def\specialization#1{\gdef\@specialization{#1}}
\def\noAlbum#1{\gdef\@noAlbum{#1}}
\def\thesisDone#1{\gdef\@thesisDone{#1}}

% Footer left text (optional). Keep page number in center.
\newcommand{\thesisheaders}{%
  \fancyfoot[L]{\small \@shortauthor\quad\textit{\@shorttitlePL}}%
  \fancyfoot[C]{\fontsize{12}{14}\selectfont\thepage}%
}

%---------------------------------------------------------------------------
% CAPTIONS (10pt) + labels "Rys." and "Tab." + source line
%---------------------------------------------------------------------------

\DeclareCaptionFont{tenpt}{\fontsize{10}{12}\selectfont}
\captionsetup{
  font=tenpt,
  labelfont=bf,
  labelsep=period,
  justification=centering,
  singlelinecheck=false
}

\renewcommand{\figurename}{Rys.}
\renewcommand{\tablename}{Tab.}




%---------------------------------------------------------------------------
% TITLE PAGE (number hidden, but page counted)
% IMPORTANT: do NOT insert extra blank pages here (it breaks TOC=page 2)
%---------------------------------------------------------------------------

\newcommand{\titlepages}{%
  \clearpage
  \begin{titlepage}
    \thispagestyle{empty} % hide page number on title page, but page is counted

    % Logo (left)
    \begin{flushleft}
      \includegraphics[scale=0.7]{figures/PL_main.jpg}
    \end{flushleft}

    \begin{center}
    \ifCLASSOPTIONpl

      \vspace*{5mm}
      {\fontsize{16}{18}\selectfont\bfseries KOLEGIUM INFORMATYKI STOSOWANEJ}

      \vspace*{10mm}
      \begin{tabular}{ll}
        {\fontsize{12}{14}\selectfont\bfseries Kierunek:}
        & {\fontsize{12}{14}\selectfont\bfseries \@degreeprogramme}\\
        {\fontsize{12}{14}\selectfont\bfseries Specjalność:}
        & {\fontsize{12}{14}\selectfont\bfseries \@specialization}
      \end{tabular}

      \vspace*{25mm}
      {\fontsize{14}{16}\selectfont \@author}

      {\fontsize{14}{16}\selectfont Nr albumu studenta: \@noAlbum}

      \vspace*{10mm}
      {\fontsize{20}{24}\selectfont\bfseries\itshape \@titlePL}

      \vspace*{10mm}
      \begin{tabular}{ll}
        {\fontsize{14}{16}\selectfont Prowadząca:}
        & {\fontsize{14}{16}\selectfont \@supervisor}\\
        \ifCLASSOPTIONauxsup
          % {\fontsize{14}{16}\selectfont Promotor pomocniczy:}
          % & {\fontsize{14}{16}\selectfont \@secondsupervisor}
        \fi
      \end{tabular}

      \vfill
      {\fontsize{18}{22}\selectfont\bfseries \@thesistype}

      \vfill
      {\fontsize{14}{16}\selectfont\bfseries Rzeszów \@date}

    \fi

    \ifCLASSOPTIONen
      {\fontsize{14}{16}\selectfont\bfseries Rzeszów \@date}
    \fi
    \end{center}
  \end{titlepage}
}

\makeatother


