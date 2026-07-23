Aplikace nefunguje v prohlížeči brave - window.open() otevře okno pro ověření a po zavření program skončí s chybou -1 => nejspíše problém s nastavením zabezpečení v prohlížeči.
Chrome funguje bez problému

Je třeba vytvořit vlastní přihlašovací údaje k API na stránkách poskytovatele a vložit je do VaclavikBC/appsettings.json (ClientID a ClientSecret). Oprávnění musí být na čtení všech dat kalendáře.
