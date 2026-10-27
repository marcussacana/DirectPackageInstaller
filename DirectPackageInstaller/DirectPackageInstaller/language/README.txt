DirectPackageInstaller - language packs
======================================

Every *.ini file in this folder is a language. There is no list of supported
languages in the code: drop a new .ini file in here and it shows up in the
Options > Language menu, no rebuild needed. The menu is rescanned every time it
is opened, so you can add a file while DirectPackageInstaller is running and
just reopen the menu.


Adding a new language
---------------------

1. Copy English.ini into this folder and rename it. The file name is the
   language id, it is what gets saved to Settings.ini, so keep it short and
   without spaces (Japanese.ini, BrazilianPortuguese.ini, ...).

2. Fill the [Meta] section:

       [Meta]
       Name=Japanese
       NativeName=日本語

   Name        the language name in English, used to tell packs apart when two
               of them share the same NativeName
   NativeName  the language name as its own speakers write it, this is what
               the user sees in the menu

3. Translate the [Language] section below. Keep the keys exactly as they are,
   only the text after the = changes. A pack needs at least one translated
   string to be listed in the menu, a file with nothing but a [Meta] header is
   ignored, that way a stray backup or a notes file can't end up in the menu.

4. Open Options > Language in DirectPackageInstaller and pick your language. It
   is saved to Settings.ini and restored on the next start.


You don't have to translate everything
-------------------------------------

English.ini is the fallback for every other pack, so a partially translated file
is perfectly fine. Any key you leave out (or leave empty) is shown in English
until someone fills it in. English.ini itself is the reference for the full key
list, don't remove keys from your own file, just drop the ones you can't
translate yet.


Formatting
----------

  \n         line break inside a value
  \t         tab
  {0} {1}    placeholder, filled in by the app, for example the IP address or
             the transferred size. Keep them, the order is yours to choose as
             long as it makes sense for your language.

Keys with placeholders are the ones that show numbers, such as
Status.TransferSending or Welcome.Firewall. If you change the order of {0} and
{1}, also change the order of the words around them.


The fallback file
-----------------

English.ini is special: it is the fallback of all the other packs, so it should
stay complete. It is the only language that isn't discovered by scanning the
folder, its name is referenced directly by the code.
