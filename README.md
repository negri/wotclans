# WoTClans
The source code for the [World of Tanks for Console Clans Site](https://wotclans.com.br/) and [Discord Bot](https://wotclans.com.br/DiscordBot)

## What is this?

It's the full source code of the site (and related tools) [WotClans](https://wotclans.com.br/). It tracks the performance of clan players on the
[World of Tanks](https://console.worldoftanks.com/) (for consoles) game, by Wargaming.

## Architeture

### Constraints & History

To understand the choosen architeture it's relevant to understand some of the history of the site, and the costs 
constraints that were applied everywhere.

The site started because I wanted to easily track my performance, and of my some of my clan, without entering the 
pages on [WoTInfo](http://wotinfo.net/en) to every player.

I setup an agreement with Stan (WoTInfo owner) that kindly allowed me to query their pages, and I did so for many years. 
You can still find code that parses his pages, altought I now calculate my own WN8 values.

The other major constraint for the site was **cost of running** it. I live in Brazil, and altought (for my country standards) 
I'm rich enough to dedicate my time to the site, and *foot the bill*, I can't pay much for hosting, even on Amazon or Azure. 
So I payed for the cheapest hosting I could find, on [SmarterASP](https://www.smarterasp.net/).

Their service, despise being cheap, is very good. But this site needs a lot of database and some continuing running proccesses,
and that makes it not cheap.

But it happens that I own a (small!) [company](https://elekto.com.br/) in Brazil and, with the agreement of my partners, I started to use spare CPU time and Database space
on our backup server.

So, the basic was configured: On my own servers I keep the *main database*, and the data collector and calculations proccesses; and on the
remote site I keep only static files containing the data to be displayed.

Latter, when I started to compute my own WN8, I had to use a database on the remote server to keep the player data, as it would be a problem
keep more than 50k files on the file system.

The site, now, is not so cheap to run, but thanks to the great support of the community I'm receiving donations enought to cover most of 
the hosting costs.

### Components

* Main Database: A currently 25GB (with lots of page compression) Sql Server 2016 db that hosts clans, players, tanks statistics etc.
* Site Database: A currently 2GB Sql Server 2016 db that is more like a *key store* to holds *by player* data.
* FetchPlayers: A console app that runs all the time querying player data from the [Wargaming API](https://developers.wargaming.net/).
* FetchClanMembership: A console app that runs hourly to query clan data from the [Wargaming API](https://developers.wargaming.net/).
* CalculateClanStats: A console app thar runs hourly to calculate clan data and updates the files on the Site.
* Site: The main site of the application. There are 2 deploys, [one for XBOX](https://wotclans.com.br/), and [another to PS4](https://ps.wotclans.com.br/).
* WotClansBot: a Discord bot that runs on my own server, with direct access to the *Main Database* that is a nice complement to the Site.

## Database

### Main Database

It holds everything!

I will soon(tm) provide a link to the daily backups and some explanation on it. 

### Site Database

It's just a key store from the player ID to a compressed json that represents overall, last month and last week data for the player.

It's updated by the FetchPlayers (and the WotClansBot, sometimes) by using an administrative API on the site.

## Contribute

Why not? Altought this is more like a "full disclosure" iniciative more than giving away control of the project, feel free to contact me. There are a few places
that I would love to have some help:

* Translations to German, Russian, Spanish...
* A more "mobile friendly" main view...
* etc

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/A0A4ITI9T)

## FAQ

1) Can I copy your project, database, and open up my version of it with beer, hookers and ads?

   Sure! By all means! It's MIT licensed.

2) There are comments in Spanish on the code!

   Actually they are in Portuguese, my usual language. They will be translated, eventually.

3) Your code sucks! Will you improve it?

   Nah! It has a purpose, a reason, and works very fine giving the constraints of cost in running and maintening the site. But of course I will improve it, and suggestions
   on how to inprove, refactor and so on are wellcome.

4) I love you! Thanks! Can I use snippets of your code?

   I love you too! And sure, You can!

