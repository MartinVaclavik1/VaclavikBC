namespace VaclavikBC.Models
{
    public class Calendar
    {
        /*
           "kind": "calendar#calendarListEntry",
           "etag": "\"1773067877088367\"",
           "id": "bcpracemartin@gmail.com",
           "summary": "bcpracemartin@gmail.com",
           "timeZone": "Europe/Prague",
           "colorId": "14",
           "backgroundColor": "#9fe1e7",
           "foregroundColor": "#000000",
           "selected": true,
           "accessRole": "owner"
        */
        public int Id { get; set; }
        public string IDProvider { get; set; }  //když bude aktualizace, tak se bude kontrolovat id kalendáře
                                                //a načtou se znovu jen eventy => selected zůstene stejný 
        public string Name { get; set; } //summary
        public string TimeZone { get; set; } //příklad: Europe/prague
        public string BackgroundColor { get; set; }
        public string ForegroundColor { get; set; }
        public bool Selected { get; set; }
        public List<CalendarEvent> Events { get; set; } = new List<CalendarEvent>();
        public bool DontDeleteCheck { get; set; } //když se smaže v originálním kalendáři, tak se
                                                  //při aktualizaci zeptá na smazání. Při další aktualizaci
                                                  //se koukne na tuto podmínku a nezeptá se, protože bude
                                                  //vědět, že se již ptal
    }
}
