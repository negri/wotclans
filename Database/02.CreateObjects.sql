USE [WoTClans]
GO
CREATE SCHEMA [Achievements]
GO
CREATE SCHEMA [Current]
GO
CREATE SCHEMA [Discord]
GO
CREATE SCHEMA [Historic]
GO
CREATE SCHEMA [Main]
GO
CREATE SCHEMA [Performance]
GO
CREATE SCHEMA [Store]
GO
CREATE SCHEMA [Support]
GO
CREATE SCHEMA [Tanks]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

create function [dbo].[Max2Float] (@v1 as float, @v2 as float)
returns float
as
begin
	if (@v1 >= @v2) return @v1;
	return @v2;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create function [dbo].[Max2Values] (@v1 as bigint, @v2 as bigint)
returns bigint
as
begin
	if (@v1  > @v2) return @v1;
	return @v2;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE function [Main].[GetUpdateDelay](@lastHours as int)
returns float
as
begin
	declare @ud float;
	select 
			--count(1) as NumPlayers,
			@ud = avg(DATEDIFF(minute, Moment, NextMoment)/60.0)-- as DiffQueryHoursAvg
		from
		(
		select --top 100
			pd.PlayerId,
				pd.Moment, lag(pd.Moment) over (partition by pd.PlayerId order by pd.Moment desc) as NextMoment
			from [Main].[PlayerDate] pd
				inner join 
				(
					select PlayerId from [Main].[RecentClanCompositionStats] where [Delay] = 1 and PlayerDelay = 1
				) d1 
					on d1.PlayerId = pd.PlayerId 
			where pd.Moment >= DATEADD(hour, -@lastHours, getutcdate())
		) q
		where q.NextMoment is not null;

	if (@ud is null)
	begin
		set @ud = @lastHours;
	end;

	return @ud;
end;
	
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Tanks].[Nation](
	[NationId] [int] NOT NULL,
	[Nation] [nvarchar](15) NOT NULL,
 CONSTRAINT [PK_Nation] PRIMARY KEY CLUSTERED 
(
	[NationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Tanks].[Type](
	[TypeId] [int] NOT NULL,
	[Type] [nvarchar](15) NOT NULL,
 CONSTRAINT [PK_Type] PRIMARY KEY CLUSTERED 
(
	[TypeId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Tanks].[Tank](
	[TankId] [bigint] NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[ShortName] [nvarchar](15) NOT NULL,
	[NationId] [int] NOT NULL,
	[Tier] [int] NOT NULL,
	[TypeId] [int] NOT NULL,
	[Tag] [nvarchar](50) NOT NULL,
	[IsPremium] [bit] NOT NULL,
	[IsJoke] [bit] NOT NULL,
	[InclusionMoment] [datetime] NOT NULL,
	[AgeDays]  AS (datediff(day,[InclusionMoment],getutcdate())),
 CONSTRAINT [PK_Tank] PRIMARY KEY CLUSTERED 
(
	[TankId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Main].[Player](
	[PlayerId] [bigint] NOT NULL,
	[GamerTag] [nvarchar](25) NOT NULL,
	[PlataformId] [int] NOT NULL,
	[Delay] [float] NOT NULL,
	[InclusionMoment] [datetime] NOT NULL,
	[AgeDays]  AS (datediff(day,[InclusionMoment],getutcdate())),
 CONSTRAINT [PK_Player] PRIMARY KEY CLUSTERED 
(
	[PlayerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Performance].[PlayerDate](
	[PlayerId] [bigint] NOT NULL,
	[Date] [date] NOT NULL,
	[Moment] [datetime] NOT NULL,
	[TankId] [bigint] NOT NULL,
	[LastBattle] [datetime] NOT NULL,
	[TreesCut] [bigint] NOT NULL,
	[MaxFrags] [bigint] NOT NULL,
	[MarkOfMastery] [bigint] NOT NULL,
	[BattleLifeTimeSeconds] [bigint] NOT NULL,
	[Spotted] [bigint] NOT NULL,
	[DamageAssistedTrack] [bigint] NOT NULL,
	[DamageAssistedRadio] [bigint] NOT NULL,
	[DamageAssisted]  AS ([DamageAssistedTrack]+[DamageAssistedRadio]),
	[CapturePoints] [bigint] NOT NULL,
	[DroppedCapturePoints] [bigint] NOT NULL,
	[Battles] [bigint] NOT NULL,
	[Wins] [bigint] NOT NULL,
	[Losses] [bigint] NOT NULL,
	[Draws]  AS (([Battles]-[Wins])-[Losses]),
	[WinRatio]  AS (((1.0)*[Wins])/[Battles]),
	[Kills] [bigint] NOT NULL,
	[SurvivedBattles] [bigint] NOT NULL,
	[Deaths]  AS ([Battles]-[SurvivedBattles]),
	[DamageDealt] [bigint] NOT NULL,
	[DamageReceived] [bigint] NOT NULL,
	[TotalDamage]  AS (([DamageAssistedTrack]+[DamageAssistedRadio])+[DamageDealt]),
	[PiercingsReceived] [bigint] NOT NULL,
	[Hits] [bigint] NOT NULL,
	[NoDamageDirectHitsReceived] [bigint] NOT NULL,
	[ExplosionHits] [bigint] NOT NULL,
	[Piercings] [bigint] NOT NULL,
	[Shots] [bigint] NOT NULL,
	[ExplosionHitsReceived] [bigint] NOT NULL,
	[XP] [bigint] NOT NULL,
	[DirectHitsReceived] [bigint] NOT NULL,
	[IsAnchor] [bit] NOT NULL,
 CONSTRAINT [PK_PerformancePlayerDate] PRIMARY KEY CLUSTERED 
(
	[Date] ASC,
	[TankId] ASC,
	[PlayerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF, DATA_COMPRESSION = PAGE) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Função para retornar os jogadores atualizados até uma certa data
CREATE function [Performance].[GetRecentPlayerUpTo]
(
	@date date -- somente dados anteriores, ou na mesma data, serão considerados
)
returns table
return
    select  
			q.PlayerId, p.GamerTag, 
			q.TankId, t.Tier, t.ShortName, n.Nation, tt.[Type], t.IsPremium,
			q.[Date], q.Moment, 
			LastBattle, TreesCut, MaxFrags, MarkOfMastery,
			[BattleLifeTimeSeconds], [Spotted], [DamageAssistedTrack], [DamageAssistedRadio],
			[DamageAssisted], [CapturePoints], [DroppedCapturePoints], [Battles], [Wins], [Losses],
			[Draws], [WinRatio], [Kills], [SurvivedBattles], [Deaths], [DamageDealt], [DamageReceived], [TotalDamage]
    from
    (
    select 
			PlayerId, TankId, [Date], Moment, LastBattle, TreesCut, MaxFrags, MarkOfMastery,
			[BattleLifeTimeSeconds], [Spotted], [DamageAssistedTrack], [DamageAssistedRadio],
			[DamageAssisted], [CapturePoints], [DroppedCapturePoints], [Battles], [Wins], [Losses],
			[Draws], [WinRatio], [Kills], [SurvivedBattles], [Deaths], [DamageDealt], [DamageReceived], [TotalDamage],
			ROW_NUMBER() over (partition by PlayerId, TankId order by [Date] desc) as DateOrder
	   from Performance.[PlayerDate] pd
	   where [Date] <= @date
    ) q
	inner join Main.Player p
		on p.PlayerId = q.PlayerId
	inner join Tanks.Tank t
		on (t.TankId = q.TankId) 
	inner join Tanks.Nation n
		on n.NationId = t.NationId
	inner join Tanks.[Type] tt
		on tt.TypeId = t.TypeId
    where DateOrder = 1;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE function [Performance].[GetDelta]
(
	@antDate date, -- data anterior, inclusive
	@posDate date  -- data posterior, inclusive
)
returns table
return
select --*,		
		p.GamerTag, tt.ShortName as [Tank], tt.Tier,
		(d.Battles - ISNULL(a.Battles, d.Battles)) as Battles, 
		(d.BattleLifeTimeSeconds - ISNULL(a.BattleLifeTimeSeconds, d.BattleLifeTimeSeconds))/60.0/60.0 as [Hours],
		(d.TotalDamage - ISNULL(a.TotalDamage, d.TotalDamage))/1.0/(d.Battles - ISNULL(a.Battles, d.Battles)) as [AvgTotalDmg],				
		d.PlayerId, d.TankId, tt.IsPremium
	from Performance.GetRecentPlayerUpTo(@posDate) d
		left outer join Performance.GetRecentPlayerUpTo(@antDate) a
			on (a.PlayerId = d.PlayerId) and (a.TankId = d.TankId)
		inner join Tanks.Tank tt
			on (d.TankId = tt.TankId)
		inner join Main.Player p
			on (d.PlayerId = p.PlayerId)			
	where
		((d.Battles - ISNULL(a.Battles, d.Battles)) > 0);
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE function [Performance].[GetTanksStatsForWn8]
(
	@playerId bigint, 
	@date date = null
)
returns table
as
return
select  --q.*,
		@playerId as PlayerId, q.TankId, 
		Battles, 
		-- WN8
		DamageDealt as Damage,
		Wins  as Win,
		Kills as Frag,		
		Spotted as Spot,		
		DroppedCapturePoints as Def,
		-- Atack
		DamageAssistedTrack, DamageAssistedRadio, Shots, Hits, Piercings, ExplosionHits, CapturePoints, Losses, 
		-- Defense
		DamageReceived, SurvivedBattles, NoDamageDirectHitsReceived, DirectHitsReceived, ExplosionHitsReceived, PiercingsReceived,
		-- Meta
		LastBattle, TreesCut, MaxFrags, MarkOfMastery, BattleLifeTimeSeconds, XP
	from
	(
		select 
				TankId, [Battles], [DamageDealt], [Wins], [Kills], [Spotted], [DroppedCapturePoints], 
				PiercingsReceived, Hits, DamageAssistedTrack, NoDamageDirectHitsReceived, CapturePoints, ExplosionHits,
				DamageReceived, Piercings, Shots, ExplosionHitsReceived, DamageAssistedRadio, DirectHitsReceived,
				SurvivedBattles, Losses, LastBattle, TreesCut, MaxFrags, MarkOfMastery, BattleLifeTimeSeconds, XP,
				row_number() over (partition by TankId order by [Date] desc) as RowNumber
			from Performance.PlayerDate
			where 
				(PlayerId = @playerId) and ([Date] <= isnull(@date, dateadd(year, 10, getutcdate())))
	) q	
	where (RowNumber = 1) and (Battles > 0);
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE function [Performance].[GetTanksDeltaStatsForWn8]
(
	@playerId bigint, 
	@deltaDays int)
returns table
as
return
select 
		@playerId as PlayerId, c.TankId,
		c.Battles - isnull(p.Battles, 0) as Battles,
		-- WN8
		c.Damage - isnull(p.Damage, 0) as Damage,
		c.Win - isnull(p.Win, 0) as Win,
		c.Frag - isnull(p.Frag, 0) as Frag,
		c.Spot - isnull(p.Spot, 0) as Spot,
		c.Def - isnull(p.Def, 0) as Def,
		-- Atack
		c.DamageAssistedTrack - isnull(p.DamageAssistedTrack, 0) as DamageAssistedTrack,
		c.DamageAssistedRadio - isnull(p.DamageAssistedRadio, 0) as DamageAssistedRadio,
		c.Shots - isnull(p.Shots, 0) as Shots,
		c.Hits - isnull(p.Hits, 0) as Hits,
		c.Piercings - isnull(p.Piercings, 0) as Piercings,
		c.ExplosionHits - isnull(p.ExplosionHits, 0) as ExplosionHits,
		c.CapturePoints - isnull(p.CapturePoints, 0) as CapturePoints,
		c.Losses - isnull(p.Losses, 0) as Losses,
		-- Defense
		c.DamageReceived - isnull(p.DamageReceived, 0) as DamageReceived,
		c.SurvivedBattles - isnull(p.SurvivedBattles, 0) as SurvivedBattles,
		c.NoDamageDirectHitsReceived - isnull(p.NoDamageDirectHitsReceived, 0) as NoDamageDirectHitsReceived,
		c.DirectHitsReceived - isnull(p.DirectHitsReceived, 0) as DirectHitsReceived,
		c.ExplosionHitsReceived - isnull(p.ExplosionHitsReceived, 0) as ExplosionHitsReceived,
		c.PiercingsReceived - isnull(p.PiercingsReceived, 0) as PiercingsReceived,
		-- Meta
		c.LastBattle,
		c.TreesCut - isnull(p.TreesCut, 0) as TreesCut,
		c.MaxFrags,
		c.MarkOfMastery,
		c.BattleLifeTimeSeconds - isnull(p.BattleLifeTimeSeconds, 0) as BattleLifeTimeSeconds,
		p.LastBattle as PreviousLastBattle
	from Performance.GetTanksStatsForWn8(@playerId, null) c
		left outer join Performance.GetTanksStatsForWn8(@playerId, dateadd(day, -@deltaDays, getutcdate())) p
			on p.TankId = c.TankId
	where (c.Battles > p.Battles) or (p.Battles is null);
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Performance].[ReferenceValues](
	[Date] [date] NOT NULL,
	[Period] [varchar](5) NOT NULL,
	[TankId] [bigint] NOT NULL,
	[Spotted] [float] NOT NULL,
	[DamageAssistedTrack] [float] NOT NULL,
	[DamageAssistedRadio] [float] NOT NULL,
	[CapturePoints] [float] NOT NULL,
	[DroppedCapturePoints] [float] NOT NULL,
	[TotalBattles] [bigint] NULL,
	[TotalPlayers] [int] NULL,
	[TotalWins] [bigint] NULL,
	[TotalLosses] [bigint] NULL,
	[Kills] [float] NOT NULL,
	[TotalSurvivedBattles] [bigint] NULL,
	[DamageDealt] [float] NOT NULL,
	[DamageReceived] [float] NOT NULL,
	[BattleLifeTimeHours] [float] NOT NULL,
	[DamageAssisted]  AS ([DamageAssistedTrack]+[DamageAssistedRadio]),
	[TotalDraws]  AS (([TotalBattles]-[TotalWins])-[TotalLosses]),
	[WinRatio]  AS (([TotalWins]*(1.0))/[TotalBattles]),
	[TotalDamage]  AS (([DamageAssistedTrack]+[DamageAssistedRadio])+[DamageDealt]),
	[TreesCut] [float] NOT NULL,
	[MaxFrags] [float] NOT NULL,
	[MarkOfMastery] [float] NOT NULL,
	[Hits] [float] NOT NULL,
	[NoDamageDirectHitsReceived] [float] NOT NULL,
	[ExplosionHits] [float] NOT NULL,
	[Piercings] [float] NOT NULL,
	[Shots] [float] NOT NULL,
	[ExplosionHitsReceived] [float] NOT NULL,
	[XP] [float] NOT NULL,
	[DirectHitsReceived] [float] NOT NULL,
	[PiercingsReceived] [float] NOT NULL,
 CONSTRAINT [PK_Performance_ReferenceValues] PRIMARY KEY CLUSTERED 
(
	[Date] ASC,
	[Period] ASC,
	[TankId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF, DATA_COMPRESSION = PAGE) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO



CREATE view [Performance].[Tanks]
as
select t.ShortName, t.Tier, n.Nation, tt.[Type], t.IsPremium, t.Tag, r.*
	from Performance.ReferenceValues r
		inner join Tanks.Tank t
			on r.TankId = t.TankId 
		inner join Tanks.Nation n
			on n.NationId = t.NationId
		inner join Tanks.[Type] tt
			on tt.TypeId = t.TypeId
	where [Date] = (select max([Date]) from Performance.ReferenceValues);
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Performance].[MoE](
	[Date] [date] NOT NULL,
	[MethodId] [int] NOT NULL,
	[TankId] [bigint] NOT NULL,
	[MoE1Dmg] [float] NOT NULL,
	[MoE2Dmg] [float] NOT NULL,
	[MoE3Dmg] [float] NOT NULL,
	[MaxAvgDmg] [float] NOT NULL,
	[NumDates] [int] NOT NULL,
	[Battles] [bigint] NOT NULL,
 CONSTRAINT [PK_Performance_MoE] PRIMARY KEY CLUSTERED 
(
	[Date] ASC,
	[MethodId] ASC,
	[TankId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF, DATA_COMPRESSION = PAGE) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE view [Performance].[LastMoe]
as
select [Date], MethodId, TankId, MoE1Dmg, MoE2Dmg, MoE3Dmg, MaxAvgDmg, NumDates, Battles
	from Performance.MoE
	where [Date] = (select max([Date]) from Performance.MoE);		
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE view [Performance].[AllTanksStats]
as
select  --q.*,
		q.PlayerId, p.GamerTag, q.TankId, t.ShortName, t.Tier, tt.[Type], n.Nation,
		[Date], Moment, LastBattle, MaxFrags, MarkOfMastery, BattleLifeTimeSeconds/60.0 as BattleMinutes,
		TreesCut*1.0/Battles as TreesCut, 
		Spotted*1.0/Battles as Spotted,
		DamageAssistedTrack*1.0/Battles as DamageAssistedTrack,
		DamageAssistedRadio*1.0/Battles as DamageAssistedRadio,
		DamageAssisted*1.0/Battles as DamageAssisted,
		CapturePoints*1.0/Battles as CapturePoints,
		DroppedCapturePoints*1.0/Battles as DroppedCapturePoints,
		Battles, Wins*1.0/Battles as WinRatio,
		Kills*1.0/Battles as Kills,
		SurvivedBattles*1.0/Battles as SurvivedRatio,
		Kills*1.0/nullif(Deaths, 0) as KillDeathRatio,
		DamageDealt*1.0/Battles as DamageDealt,
		DamageReceived*1.0/Battles as DamageReceived,
		DamageDealt*1.0/nullif(DamageReceived, 0) as DamageRatio,
		TotalDamage*1.0/Battles as TotalDamage,
		DirectHitsReceived*1.0/Battles as DirectHitsReceived,
		PiercingsReceived*1.0/Battles as PiercingsReceived,
		NoDamageDirectHitsReceived*1.0/Battles as NoDamageDirectHitsReceived,
		ExplosionHitsReceived*1.0/Battles as ExplosionHitsReceived,		
		Shots*1.0/Battles as Shots,
		Hits*1.0/Battles as Hits,
		Piercings*1.0/Battles as Piercings,
		ExplosionHits*1.0/Battles as ExplosionHits,
		Hits*1.0/nullif(Shots, 0) as HitRatio,
		Piercings*1.0/nullif(Shots, 0) as PiercingRatio,
		DamageDealt*1.0/nullif(Piercings, 0) as DamagePerPiercing,
		DamageDealt*1.0/BattleLifeTimeSeconds*60.0 as DamagePerMinute,
		XP,
		XP*1.0/BattleLifeTimeSeconds*60.0 as XPPerMinute
	from
	(
		select *, 
			row_number() over (partition by PlayerId, TankId order by [Date] desc) as RowNumber
			from Performance.PlayerDate			
	) q
	inner join Tanks.Tank t
		on (t.TankId = q.TankId)
	inner join Tanks.[Type] tt
		on (t.TypeId = tt.TypeId)
	inner join Tanks.Nation n
		on (n.NationId = t.NationId)
	inner join Main.Player p
		on (p.PlayerId = q.PlayerId)
	where (RowNumber = 1) and (Battles > 0) and (Shots > 0) and (BattleLifeTimeSeconds > 0);
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Main].[Clan](
	[ClanTag] [varchar](5) NOT NULL,
	[Name] [nvarchar](70) NOT NULL,
	[FlagCode] [char](2) NULL,
	[Enabled] [bit] NOT NULL,
	[Delay] [float] NOT NULL,
	[ClanId] [bigint] NOT NULL,
	[IsHidden] [bit] NOT NULL,
	[DisabledReason] [int] NOT NULL,
	[InclusionMoment] [datetime] NOT NULL,
	[AgeDays]  AS (datediff(day,[InclusionMoment],getutcdate())),
 CONSTRAINT [PK_Clan] PRIMARY KEY CLUSTERED 
(
	[ClanId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Main].[ClanDate](
	[Date] [date] NOT NULL,
	[MembershipMoment] [datetime] NOT NULL,
	[CalculationMoment] [datetime] NULL,
	[TotalMembers] [int] NULL,
	[ActiveMembers] [int] NULL,
	[TotalBattles] [int] NULL,
	[WeekBattles] [int] NULL,
	[TotalWinRate] [float] NULL,
	[MonthWinrate] [float] NULL,
	[WeekWinRate] [float] NULL,
	[TotalWN8] [float] NULL,
	[WN8a] [float] NULL,
	[WN8t15] [float] NULL,
	[WN8t7] [float] NULL,
	[MonthBattles] [int] NULL,
	[Top15AvgTier] [float] NOT NULL,
	[ActiveAvgTier] [float] NOT NULL,
	[TotalAvgTier] [float] NOT NULL,
	[ClanId] [bigint] NOT NULL,
 CONSTRAINT [PK_ClanDate] PRIMARY KEY CLUSTERED 
(
	[ClanId] ASC,
	[Date] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE view [Main].[RecentClanDateCalculation]
as
select ClanId, ClanTag, CalculationMoment, 
	isnull(DATEDIFF(hour, CalculationMoment, GETUTCDATE()), 6969) as CalculationAgeHours,
	TotalMembers, ActiveMembers, WN8t15, [Date]
	from
	(
		select 
				c.ClanTag, c.ClanId, cd.[Date], cd.CalculationMoment,
				ROW_NUMBER() over (partition by c.ClanId order by cd.CalculationMoment desc) as [Order],
				cd.TotalMembers, cd.ActiveMembers, cd.WN8t15
			from Main.ClanDate cd
				inner join Main.Clan c
					on (c.ClanId = cd.ClanId)
			where c.[Enabled] = 1
	) q where [Order] = 1;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Tanks].[PcTank](
	[TankId] [bigint] NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[ShortName] [nvarchar](15) NOT NULL,
	[NationId] [int] NOT NULL,
	[Tier] [int] NOT NULL,
	[TypeId] [int] NOT NULL,
	[Tag] [nvarchar](50) NOT NULL,
	[IsPremium] [bit] NOT NULL,
	[IsJoke] [bit] NOT NULL,
	[InclusionMoment] [datetime] NOT NULL,
	[AgeDays]  AS (datediff(day,[InclusionMoment],getutcdate())),
 CONSTRAINT [PK_PcTank] PRIMARY KEY CLUSTERED 
(
	[TankId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Tanks].[Wn8XvmSurrogate](
	[TankId] [bigint] NOT NULL,
	[SurrogateTankId] [bigint] NOT NULL,
	[SecondSurrogateTankId] [bigint] NULL,
	[Reason] [nvarchar](max) NULL,
 CONSTRAINT [PK_Tanks_Wn8Surrogate] PRIMARY KEY CLUSTERED 
(
	[TankId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE view [Tanks].[AllWn8Surrogate]
as
select 
		c.TankId, c.ShortName, c.[Name], c.Tag, c.Tier, c.TypeId, tt.[Type] , c.NationId, n.Nation,
		pc.TankId as PCTankId, pc.ShortName as PCShortName, pc.[Name] as PCName, pc.Tag as PCTag, pc.Tier as PCTier, pc.TypeId as PCTypeId, pc.NationId as PCNationId,
		pc2.TankId as PC2TankId, pc2.ShortName as PC2ShortName, pc2.[Name] as PC2Name, pc2.Tag as PC2Tag, pc2.Tier as PC2Tier, pc2.TypeId as PC2TypeId, pc2.NationId as PC2NationId
	from Tanks.Wn8XvmSurrogate s
		inner join Tanks.Tank c
			on (s.TankId = c.TankId)
		inner join Tanks.PcTank pc
			on (s.SurrogateTankId = pc.TankId) 
		left outer join Tanks.PcTank pc2
			on (s.SecondSurrogateTankId = pc2.TankId)
		inner join Tanks.Nation n
			on n.NationId = c.NationId
		inner join Tanks.[Type] tt
			on tt.TypeId = c.TypeId;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE function [Performance].[GetTanksStatsHist]
(
@playerId bigint, 
@tankId bigint
)
returns table
as
return
select  --q.*,
		q.PlayerId, p.GamerTag, q.TankId, t.ShortName, t.Tier, tt.[Type], n.Nation,		
		[Date], Moment, LastBattle, MaxFrags, MarkOfMastery, BattleLifeTimeSeconds/60.0 as BattleMinutes,
		TreesCut*1.0/Battles as TreesCut, 
		Spotted*1.0/Battles as Spotted,
		DamageAssistedTrack*1.0/Battles as DamageAssistedTrack,
		DamageAssistedRadio*1.0/Battles as DamageAssistedRadio,
		DamageAssisted*1.0/Battles as DamageAssisted,
		CapturePoints*1.0/Battles as CapturePoints,
		DroppedCapturePoints*1.0/Battles as DroppedCapturePoints,
		Battles, Wins*1.0/Battles as WinRatio,
		Kills*1.0/Battles as Kills,
		SurvivedBattles*1.0/Battles as SurvivedRatio,
		Kills*1.0/nullif(Deaths, 0) as KillDeathRatio,
		DamageDealt*1.0/Battles as DamageDealt,
		DamageReceived*1.0/Battles as DamageReceived,
		DamageDealt*1.0/nullif(DamageReceived, 0) as DamageRatio,
		TotalDamage*1.0/Battles as TotalDamage,
		DirectHitsReceived*1.0/Battles as DirectHitsReceived,
		PiercingsReceived*1.0/Battles as PiercingsReceived,
		NoDamageDirectHitsReceived*1.0/Battles as NoDamageDirectHitsReceived,
		ExplosionHitsReceived*1.0/Battles as ExplosionHitsReceived,		
		Shots*1.0/Battles as Shots,
		Hits*1.0/Battles as Hits,
		Piercings*1.0/Battles as Piercings,
		ExplosionHits*1.0/Battles as ExplosionHits,
		Hits*1.0/nullif(Shots, 0) as HitRatio,
		Piercings*1.0/nullif(Shots, 0) as PiercingRatio,
		DamageDealt*1.0/nullif(Piercings, 0) as DamagePerPiercing,
		DamageDealt*1.0/BattleLifeTimeSeconds*60.0 as DamagePerMinute,
		XP,
		XP*1.0/BattleLifeTimeSeconds*60.0 as XPPerMinute,
		tt.TypeId, n.NationId, t.IsPremium, t.Tag, t.[Name]
	from Performance.PlayerDate q
	inner join Tanks.Tank t
		on (t.TankId = q.TankId)
	inner join Tanks.[Type] tt
		on (t.TypeId = tt.TypeId)
	inner join Tanks.Nation n
		on (n.NationId = t.NationId)
	inner join Main.Player p
		on (p.PlayerId = q.PlayerId)
	where (q.PlayerId = @playerId) and (q.TankId = @tankId)
		and (Battles > 0) and (Shots > 0) and (BattleLifeTimeSeconds > 0);
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE function [Performance].[GetTanksStatsForWn8Hist]
(
	@playerId bigint, 
	@tankId bigint)
returns table
as
return
select  --q.*,
		@playerId as PlayerId, q.TankId, 
		Battles, 
		-- WN8
		DamageDealt as Damage,
		Wins  as Win,
		Kills as Frag,		
		Spotted as Spot,		
		DroppedCapturePoints as Def,
		-- Atack
		DamageAssistedTrack, DamageAssistedRadio, Shots, Hits, Piercings, ExplosionHits, CapturePoints, Losses, 
		-- Defense
		DamageReceived, SurvivedBattles, NoDamageDirectHitsReceived, DirectHitsReceived, ExplosionHitsReceived, PiercingsReceived,
		-- Meta
		LastBattle, TreesCut, MaxFrags, MarkOfMastery, BattleLifeTimeSeconds
	from Performance.PlayerDate q
	where (PlayerId = @playerId) and (Battles > 0) and (q.TankId = @tankId);
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Current].[Clan](
	[ClanId] [bigint] NOT NULL,
	[MembershipMoment] [datetime] NOT NULL,
	[CalculationMoment] [datetime] NULL,
	[TotalMembers] [int] NULL,
	[ActiveMembers] [int] NULL,
	[TotalBattles] [int] NULL,
	[MonthBattles] [int] NULL,
	[TotalWinRate] [float] NULL,
	[MonthWinrate] [float] NULL,
	[TotalWN8] [float] NULL,
	[WN8a] [float] NULL,
	[WN8t15] [float] NULL,
	[WN8t7] [float] NULL,
	[CalculationAgeHours]  AS (datediff(hour,[CalculationMoment],getutcdate())),
	[Date]  AS (CONVERT([date],isnull([CalculationMoment],[MembershipMoment]))),
	[MembershipAgeHours]  AS (datediff(hour,[MembershipMoment],getutcdate())),
	[Top15AvgTier] [float] NOT NULL,
	[ActiveAvgTier] [float] NOT NULL,
	[TotalAvgTier] [float] NOT NULL,
 CONSTRAINT [PK_Current_Clan] PRIMARY KEY CLUSTERED 
(
	[ClanId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


CREATE view [Main].[RecentClanDate]
as
select 
		c.ClanTag, c.[Name], c.FlagCode, c.[Enabled], c.[Delay], 
		cd.[Date], 
		cd.MembershipMoment, cd.CalculationMoment, 
		cd.TotalMembers, cd.ActiveMembers, 
		cd.TotalBattles, cd.MonthBattles, 0 as WeekBattles,
		cd.TotalWinRate, cd.MonthWinrate, 0.0 as WeekWinRate, 
		cd.TotalWN8, cd.WN8a, cd.WN8t15, cd.WN8t7,
		cd.CalculationAgeHours,
		cd.ClanId, c.IsHidden
    from
		Main.Clan c
			inner join [Current].Clan cd
				on (c.ClanId = cd.ClanId);
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Current].[ClanPlayer](
	[ClanId] [bigint] NOT NULL,
	[PlayerId] [bigint] NOT NULL,
	[RankId] [int] NOT NULL,
 CONSTRAINT [PK_Current_ClanPlayer] PRIMARY KEY CLUSTERED 
(
	[ClanId] ASC,
	[PlayerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE view [Main].[RecentClanComposition]
as
select  rcd.ClanTag, rcd.[Name], FlagCode, [Enabled], rcd.[Delay], rcd.[Date], MembershipMoment,
	   cdp.PlayerId, cdp.RankId, p.GamerTag, rcd.ClanId, rcd.IsHidden, p.PlataformId as PlayerPlatformId
    from Main.RecentClanDate rcd
	   inner join [Current].ClanPlayer cdp
		  on (cdp.ClanId = rcd.ClanId)
	   inner join Main.Player p
		  on (p.PlayerId = cdp.PlayerId)
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Current].[Player](
	[PlayerId] [bigint] NOT NULL,
	[Moment] [datetime] NOT NULL,
	[TotalBattles] [int] NOT NULL,
	[MonthBattles] [int] NOT NULL,
	[TotalWinRate] [float] NOT NULL,
	[MonthWinRate] [float] NOT NULL,
	[TotalWN8] [float] NOT NULL,
	[MonthWN8] [float] NOT NULL,
	[IsPatched] [bit] NOT NULL,
	[Origin] [int] NOT NULL,
	[Date]  AS (CONVERT([date],[Moment])),
	[AgeHours]  AS (datediff(hour,[Moment],getutcdate())),
	[TotalTier] [float] NOT NULL,
	[MonthTier] [float] NOT NULL,
	[Tier10TotalBattles] [int] NOT NULL,
	[Tier10MonthBattles] [int] NOT NULL,
	[Tier10TotalWinRate] [float] NOT NULL,
	[Tier10MonthWinRate] [float] NOT NULL,
	[Tier10TotalWN8] [float] NOT NULL,
	[Tier10MonthWN8] [float] NOT NULL,
 CONSTRAINT [PK_Current_Player] PRIMARY KEY CLUSTERED 
(
	[PlayerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE view [Main].[RecentPlayer]
as    
    select --top (100) 
			c.PlayerId, c.[Date], c.Moment, 
			c.TotalBattles, c.MonthBattles, 
			c.TotalWinRate, c.MonthWinRate, 
			c.TotalWN8, c.MonthWN8, c.IsPatched, p.PlataformId, p.GamerTag, p.[Delay], c.Origin,
			c.TotalTier, c.MonthTier
    from
		[Current].Player c    
	inner join Main.Player p
		on p.PlayerId = c.PlayerId;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO




CREATE view [Main].[RecentClanCompositionStats]
as
select  
		rcc.ClanTag, rcc.[Name], rcc.FlagCode, rcc.[Enabled], rcc.[Delay], rcc.[Date], rcc.MembershipMoment,
		rcc.PlayerId, rcc.RankId, rcc.GamerTag,
		rp.[Date] as PlayerDate, rp.Moment as PlayerMoment, rp.TotalBattles, rp.MonthBattles, 
		rp.TotalWinRate, rp.MonthWinRate, rp.TotalWN8, rp.MonthWN8, 
		rp.IsPatched, rcc.ClanId, rp.[Delay] as PlayerDelay,
		(case when (rp.MonthBattles >= 21) then (1) else (0) end) as IsActive,
		(rcc.[Delay] * rp.[Delay]) as TotalDelay, rcc.IsHidden, rp.Origin,
		rp.TotalTier, rp.MonthTier, rcc.PlayerPlatformId
    from Main.RecentClanComposition rcc
	   left outer join Main.RecentPlayer rp
		  on (rp.PlayerId = rcc.PlayerId);	             
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO




CREATE view [Main].[NumberOfPlayersPerDay]
as
select 
	   sum(TotalMembers) as TotalPlayers, 
	   sum((ActiveMembers * (1.0/[Delay])) + ((TotalMembers - ActiveMembers) * (1.0/[Delay]/4.0))) as PlayersPerDay, 
	   sum((ActiveMembers * (1.0/[Delay])) + ((TotalMembers - ActiveMembers) * (1.0/[Delay]/4.0)))/24.0 as PlayersPerHour, 
	   sum((ActiveMembers * (1.0/[Delay])) + ((TotalMembers - ActiveMembers) * (1.0/[Delay]/4.0)))/24.0/60.0 as PlayersPerMinute,
	   convert(float, (select COUNT(*) from Main.RecentClanCompositionStats where DATEDIFF(MINUTE, PlayerMoment, GETUTCDATE()) <= 60*24)/24.0) as AvgPlayersPerHourLastDay,
	   convert(float, (select COUNT(*) from Main.RecentClanCompositionStats where DATEDIFF(MINUTE, PlayerMoment, GETUTCDATE()) <= 60*6)/6.0) as AvgPlayersPerHourLast6Hours,
	   convert(float, (select COUNT(*) from Main.RecentClanCompositionStats where DATEDIFF(MINUTE, PlayerMoment, GETUTCDATE()) <= 60*2)/2.0) as AvgPlayersPerHourLast2Hours,
	   convert(float, (select COUNT(*) from Main.RecentClanCompositionStats where DATEDIFF(MINUTE, PlayerMoment, GETUTCDATE()) <= 60*1)/1.0) as AvgPlayersPerHourLastHour,
	   (select count(1) from Main.Clan where [Enabled] = 1) as TotalEnabledClans,
	   (select Main.GetUpdateDelay(48)) as Last48hDelay,
	   (select Main.GetUpdateDelay(72)) as Last72hDelay,
	   (select Main.GetUpdateDelay(96)) as Last96hDelay
    from Main.RecentClanDate rcd
	   where (rcd.[Enabled] = 1) and (rcd.TotalMembers is not null);
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Main].[ClanDatePlayer](
	[Date] [date] NOT NULL,
	[PlayerId] [bigint] NOT NULL,
	[RankId] [int] NOT NULL,
	[ClanId] [bigint] NOT NULL,
 CONSTRAINT [PK_ClanDatePlayer] PRIMARY KEY CLUSTERED 
(
	[ClanId] ASC,
	[Date] ASC,
	[PlayerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF, DATA_COMPRESSION = PAGE) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Main].[PlayerDate](
	[PlayerId] [bigint] NOT NULL,
	[Date] [date] NOT NULL,
	[Moment] [datetime] NOT NULL,
	[TotalBattles] [int] NOT NULL,
	[MonthBattles] [int] NOT NULL,
	[WeekBattles] [int] NOT NULL,
	[TotalWinRate] [float] NOT NULL,
	[MonthWinRate] [float] NOT NULL,
	[WeekWinRate] [float] NOT NULL,
	[TotalWN8] [float] NOT NULL,
	[MonthWN8] [float] NOT NULL,
	[WeekWN8] [float] NOT NULL,
	[IsPatched] [bit] NOT NULL,
	[Origin] [int] NOT NULL,
	[TotalTier] [float] NOT NULL,
	[MonthTier] [float] NOT NULL,
	[Tier10TotalBattles] [int] NOT NULL,
	[Tier10MonthBattles] [int] NOT NULL,
	[Tier10TotalWinRate] [float] NOT NULL,
	[Tier10MonthWinRate] [float] NOT NULL,
	[Tier10TotalWN8] [float] NOT NULL,
	[Tier10MonthWN8] [float] NOT NULL,
 CONSTRAINT [PK_PlayerDate] PRIMARY KEY CLUSTERED 
(
	[PlayerId] ASC,
	[Date] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF, DATA_COMPRESSION = PAGE) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE function [Main].[GetPlayerHistory]
(
    @gamerTag nvarchar(16)
)
returns table
return
    select 
		  c.ClanTag, cdp.RankId,
		  p.GamerTag, p.PlataformId, p.PlayerId, pd.[Date], pd.Moment, 
		  pd.TotalBattles, pd.MonthBattles, pd.WeekBattles,
		  pd.TotalWinRate, pd.MonthWinRate, pd.WeekWinRate,
		  pd.TotalWN8, pd.MonthWN8, pd.WeekWN8,
		  DATEDIFF(MINUTE, LAG(Moment) over (order by pd.[Date]), Moment)/1440.0 as DeltaDays,
		  TotalBattles - (LAG(TotalBattles) over (order by pd.[Date])) as DeltaTotalBattles,
		  pd.IsPatched, pd.Origin, c.ClanId
	   from Main.PlayerDate pd
		  inner join Main.Player p
			 on (p.PlayerId = pd.PlayerId)
		  left outer join Main.ClanDatePlayer cdp
			 on (cdp.PlayerId = pd.PlayerId) and (cdp.[Date] = pd.[Date])
		  left outer join Main.Clan c	
			on c.ClanId = cdp.ClanId
	   where (p.GamerTag = @gamerTag);    
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO



CREATE view [Performance].[TanksAll]
as
select * from Performance.Tanks where ([Period] = 'All');
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE function [Main].[GetPlayerHistoryById]
(
    @playerId bigint
)
returns table
return
    select 
		  c.ClanTag, cdp.RankId,
		  p.GamerTag, p.PlataformId, p.PlayerId, pd.[Date], pd.Moment, 
		  pd.TotalBattles, pd.MonthBattles, pd.WeekBattles,
		  pd.TotalWinRate, pd.MonthWinRate, pd.WeekWinRate,
		  pd.TotalWN8, pd.MonthWN8, pd.WeekWN8,
		  DATEDIFF(MINUTE, LAG(Moment) over (order by pd.[Date]), Moment)/1440.0 as DeltaDays,
		  TotalBattles - (LAG(TotalBattles) over (order by pd.[Date])) as DeltaTotalBattles,
		  pd.IsPatched, pd.Origin, 
		  p.[Delay], c.ClanId, c.[Delay] as ClanDelay
	   from Main.PlayerDate pd
		  inner join Main.Player p
			 on (p.PlayerId = pd.PlayerId)
		  left outer join Main.ClanDatePlayer cdp
			 on (cdp.PlayerId = pd.PlayerId) and (cdp.[Date] = pd.[Date])
		  left outer join Main.Clan c
			on c.ClanId = cdp.ClanId
	   where (p.PlayerId = @playerId);    
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE view [Performance].[TanksMonth]
as
select * from Performance.Tanks where [Period] = 'Month';

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE view [Main].[EffectiveNumberOfPlayersPerDay]
as
    select COUNT(*) as PlayersOnLastDay, 60*24*2 as MaxPlayersOnLastDay, 
	   60*24*2 - COUNT(*) as EffecivePlayersSurplusPerDay,
	   (60*24*2 - COUNT(*))/24 as EffecivePlayersSurplusPerHour
	   from Main.RecentClanCompositionStats 
	   where DATEDIFF(MINUTE, PlayerMoment, GETUTCDATE()) <= 60*24;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Tanks].[Wn8ExpectedLoad](
	[Date] [date] NOT NULL,
	[Source] [varchar](15) NOT NULL,
	[Version] [varchar](15) NOT NULL,
	[Moment] [datetime] NOT NULL,
 CONSTRAINT [PK_Performance_Wn8ExpectedLoad] PRIMARY KEY CLUSTERED 
(
	[Date] ASC,
	[Source] ASC,
	[Version] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Tanks].[Wn8Expected](
	[Date] [date] NOT NULL,
	[Source] [varchar](15) NOT NULL,
	[Version] [varchar](15) NOT NULL,
	[TankId] [bigint] NOT NULL,
	[Def] [float] NOT NULL,
	[Frag] [float] NOT NULL,
	[Spot] [float] NOT NULL,
	[Damage] [float] NOT NULL,
	[WinRate] [float] NOT NULL,
	[Origin] [int] NOT NULL,
 CONSTRAINT [PK_Performance_Wn8Expected] PRIMARY KEY CLUSTERED 
(
	[Date] ASC,
	[Source] ASC,
	[Version] ASC,
	[TankId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE view [Tanks].[CurrentWn8Expected]
as
select 
		e.TankId, 
		t.ShortName, t.Tier, t.TypeId, tt.[Type], t.NationId, n.Nation, t.IsPremium, 
		t.Tag, t.[Name],
		e.Def, e.Frag, e.Spot, e.Damage, e.WinRate, e.Origin, e.[Source], e.[Version], e.[Date]
	from Tanks.Wn8Expected e
		inner join Tanks.Tank t
				on t.TankId = e.TankId
			inner join Tanks.[Type] tt on
				tt.TypeId = t.TypeId
			inner join Tanks.Nation n
				on n.NationId = t.NationId
	where ([Source] = 'XVM') 
		and ([Date] = (select top (1) [Date] from Tanks.Wn8ExpectedLoad order by Moment desc))
		and ([Version] = (select top (1) [Version] from Tanks.Wn8ExpectedLoad order by Moment desc))
		and (t.IsJoke = 0);
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


CREATE function [Performance].[GetTanksStats]
( 
	@playerId bigint
)
returns table
as
return
select  --q.*,
		q.PlayerId, p.GamerTag, q.TankId, t.ShortName, t.Tier, tt.[Type], n.Nation,
		[Date], Moment, LastBattle, MaxFrags, MarkOfMastery, BattleLifeTimeSeconds/60.0 as BattleMinutes,
		TreesCut*1.0/Battles as TreesCut, 
		Spotted*1.0/Battles as Spotted,
		DamageAssistedTrack*1.0/Battles as DamageAssistedTrack,
		DamageAssistedRadio*1.0/Battles as DamageAssistedRadio,
		DamageAssisted*1.0/Battles as DamageAssisted,
		CapturePoints*1.0/Battles as CapturePoints,
		DroppedCapturePoints*1.0/Battles as DroppedCapturePoints,
		Battles, Wins*1.0/Battles as WinRatio,
		Kills*1.0/Battles as Kills,
		SurvivedBattles*1.0/Battles as SurvivedRatio,
		Kills*1.0/nullif(Deaths, 0) as KillDeathRatio,
		DamageDealt*1.0/Battles as DamageDealt,
		DamageReceived*1.0/Battles as DamageReceived,
		DamageDealt*1.0/nullif(DamageReceived, 0) as DamageRatio,
		TotalDamage*1.0/Battles as TotalDamage,
		DirectHitsReceived*1.0/Battles as DirectHitsReceived,
		PiercingsReceived*1.0/Battles as PiercingsReceived,
		NoDamageDirectHitsReceived*1.0/Battles as NoDamageDirectHitsReceived,
		ExplosionHitsReceived*1.0/Battles as ExplosionHitsReceived,		
		Shots*1.0/Battles as Shots,
		Hits*1.0/Battles as Hits,
		Piercings*1.0/Battles as Piercings,
		ExplosionHits*1.0/Battles as ExplosionHits,
		Hits*1.0/nullif(Shots, 0) as HitRatio,
		Piercings*1.0/nullif(Shots, 0) as PiercingRatio,
		DamageDealt*1.0/nullif(Piercings, 0) as DamagePerPiercing,
		DamageDealt*1.0/BattleLifeTimeSeconds*60.0 as DamagePerMinute,
		XP,
		XP*1.0/BattleLifeTimeSeconds*60.0 as XPPerMinute,
		tt.TypeId, n.NationId
	from
	(
		select *, 
			row_number() over (partition by TankId order by [Date] desc) as RowNumber
			from Performance.PlayerDate
			where 
				(PlayerId = @playerId)
	) q
	inner join Tanks.Tank t
		on (t.TankId = q.TankId)
	inner join Tanks.[Type] tt
		on (t.TypeId = tt.TypeId)
	inner join Tanks.Nation n
		on (n.NationId = t.NationId)
	inner join Main.Player p
		on (p.PlayerId = q.PlayerId)
	where (RowNumber = 1) and (Battles > 0) and (Shots > 0) and (BattleLifeTimeSeconds > 0);
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


CREATE view [Tanks].[CurrentWn8ExpectedByTierType]
as
select t.Tier, t.TypeId, AVG(e.Def) as Def, AVG(e.Frag) as Frag, AVG(e.Spot) as Spot, AVG(e.Damage) as Damage, AVG(e.WinRate) as WinRate
	from Tanks.CurrentWn8Expected e
		inner join Tanks.Tank t
			on t.TankId = e.TankId and e.Origin = 0
	group by t.Tier, t.TypeId;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO



CREATE view [Tanks].[CurrentAvgExpectedByTierType]
as
select t.Tier, t.TypeId, AVG(e.DroppedCapturePoints) as Def, AVG(e.Kills) as Frag, AVG(e.Spotted) as Spot, AVG(e.DamageDealt) as Damage, AVG(e.WinRatio) as WinRate
	from Performance.TanksAll e
		inner join Tanks.Tank t
			on t.TankId = e.TankId
	group by t.Tier, t.TypeId;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE view [Tanks].[CurrentAvgWn8RelByTierType]
as
select a.Tier, a.TypeId, (a.Def/w.Def) as DefRel, (a.Frag/w.Frag) as FragRel, (a.Spot/w.Spot) as SpotRel, (a.Damage/w.Damage) as DamageRel, (a.WinRate/w.WinRate) as WinRateRel
	from [Tanks].CurrentAvgExpectedByTierType a
		inner join [Tanks].CurrentWn8ExpectedByTierType w
			on w.Tier = a.Tier and w.TypeId = a.TypeId;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE function [Performance].[GetTanksStatsUpTo]
(
@playerId bigint, 
@date date
)
returns table
as
return
select  --q.*,
		q.PlayerId, p.GamerTag, q.TankId, t.ShortName, t.Tier, tt.[Type], n.Nation,
		[Date], Moment, LastBattle, MaxFrags, MarkOfMastery, BattleLifeTimeSeconds/60.0 as BattleMinutes,
		TreesCut*1.0/Battles as TreesCut, 
		Spotted*1.0/Battles as Spotted,
		DamageAssistedTrack*1.0/Battles as DamageAssistedTrack,
		DamageAssistedRadio*1.0/Battles as DamageAssistedRadio,
		DamageAssisted*1.0/Battles as DamageAssisted,
		CapturePoints*1.0/Battles as CapturePoints,
		DroppedCapturePoints*1.0/Battles as DroppedCapturePoints,
		Battles, Wins*1.0/Battles as WinRatio,
		Kills*1.0/Battles as Kills,
		SurvivedBattles*1.0/Battles as SurvivedRatio,
		Kills*1.0/nullif(Deaths, 0) as KillDeathRatio,
		DamageDealt*1.0/Battles as DamageDealt,
		DamageReceived*1.0/Battles as DamageReceived,
		DamageDealt*1.0/nullif(DamageReceived, 0) as DamageRatio,
		TotalDamage*1.0/Battles as TotalDamage,
		DirectHitsReceived*1.0/Battles as DirectHitsReceived,
		PiercingsReceived*1.0/Battles as PiercingsReceived,
		NoDamageDirectHitsReceived*1.0/Battles as NoDamageDirectHitsReceived,
		ExplosionHitsReceived*1.0/Battles as ExplosionHitsReceived,		
		Shots*1.0/Battles as Shots,
		Hits*1.0/Battles as Hits,
		Piercings*1.0/Battles as Piercings,
		ExplosionHits*1.0/Battles as ExplosionHits,
		Hits*1.0/nullif(Shots, 0) as HitRatio,
		Piercings*1.0/nullif(Shots, 0) as PiercingRatio,
		DamageDealt*1.0/nullif(Piercings, 0) as DamagePerPiercing,
		DamageDealt*1.0/BattleLifeTimeSeconds*60.0 as DamagePerMinute,
		XP,
		XP*1.0/BattleLifeTimeSeconds*60.0 as XPPerMinute
	from
	(
		select *, 
			row_number() over (partition by TankId order by [Date] desc) as RowNumber
			from Performance.PlayerDate
			where 
				(PlayerId = @playerId) and ([Date] <= @date)
	) q
	inner join Tanks.Tank t
		on (t.TankId = q.TankId)
	inner join Tanks.[Type] tt
		on (t.TypeId = tt.TypeId)
	inner join Tanks.Nation n
		on (n.NationId = t.NationId)
	inner join Main.Player p
		on (p.PlayerId = q.PlayerId)
	where (RowNumber = 1) and (Battles > 0) and (Shots > 0) and (BattleLifeTimeSeconds > 0);
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE view [Performance].[LastBattle]
as
select PlayerId, max(LastBattle) as LastBattle 
	from [Performance].[PlayerDate]
	group by PlayerId;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Achievements].[Medal](
	[MedalCode] [varchar](25) NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[Description] [nvarchar](700) NULL,
	[HeroInformation] [nvarchar](700) NULL,
	[Condition] [nvarchar](700) NULL,
	[CategoryId] [int] NOT NULL,
	[TypeId] [int] NOT NULL,
	[SectionId] [int] NOT NULL,
	[InclusionMoment] [datetime] NOT NULL,
	[AgeDays]  AS (datediff(day,[InclusionMoment],getutcdate())),
 CONSTRAINT [PK_Achievements_Medal] PRIMARY KEY CLUSTERED 
(
	[MedalCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF, DATA_COMPRESSION = PAGE) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Achievements].[PlayerMedal](
	[PlayerId] [bigint] NOT NULL,
	[TankId] [bigint] NOT NULL,
	[MedalCode] [varchar](25) NOT NULL,
	[Count] [int] NOT NULL,
	[Battles] [bigint] NOT NULL,
 CONSTRAINT [PK_Achievements_PlayerMedal] PRIMARY KEY CLUSTERED 
(
	[PlayerId] ASC,
	[TankId] ASC,
	[MedalCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF, DATA_COMPRESSION = PAGE) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Discord].[UserPlayer](
	[UserId] [bigint] NOT NULL,
	[PlayerId] [bigint] NOT NULL,
 CONSTRAINT [PK_UserPlayer] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Historic].[MoE](
	[Date] [date] NOT NULL,
	[MethodId] [int] NOT NULL,
	[PlataformId] [int] NOT NULL,
	[TankId] [bigint] NOT NULL,
	[MoE1Dmg] [float] NOT NULL,
	[MoE2Dmg] [float] NOT NULL,
	[MoE3Dmg] [float] NOT NULL,
	[MaxAvgDmg] [float] NOT NULL,
	[NumDates] [int] NOT NULL,
	[Battles] [bigint] NOT NULL
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Historic].[ReferenceValues](
	[Date] [date] NOT NULL,
	[Period] [varchar](5) NOT NULL,
	[PlataformId] [int] NOT NULL,
	[TankId] [bigint] NOT NULL,
	[Spotted] [float] NOT NULL,
	[DamageAssistedTrack] [float] NOT NULL,
	[DamageAssistedRadio] [float] NOT NULL,
	[CapturePoints] [float] NOT NULL,
	[DroppedCapturePoints] [float] NOT NULL,
	[TotalBattles] [bigint] NULL,
	[TotalPlayers] [int] NULL,
	[TotalWins] [bigint] NULL,
	[TotalLosses] [bigint] NULL,
	[Kills] [float] NOT NULL,
	[TotalSurvivedBattles] [bigint] NULL,
	[DamageDealt] [float] NOT NULL,
	[DamageReceived] [float] NOT NULL,
	[BattleLifeTimeHours] [float] NOT NULL,
	[DamageAssisted] [float] NOT NULL,
	[TotalDraws] [bigint] NULL,
	[WinRatio] [numeric](38, 17) NULL,
	[TotalDamage] [float] NOT NULL,
	[TreesCut] [float] NOT NULL,
	[MaxFrags] [float] NOT NULL,
	[MarkOfMastery] [float] NOT NULL,
	[Hits] [float] NOT NULL,
	[NoDamageDirectHitsReceived] [float] NOT NULL,
	[ExplosionHits] [float] NOT NULL,
	[Piercings] [float] NOT NULL,
	[Shots] [float] NOT NULL,
	[ExplosionHitsReceived] [float] NOT NULL,
	[XP] [float] NOT NULL,
	[DirectHitsReceived] [float] NOT NULL,
	[PiercingsReceived] [float] NOT NULL
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Historic].[Tank](
	[PlataformId] [int] NOT NULL,
	[TankId] [bigint] NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[ShortName] [nvarchar](15) NOT NULL,
	[NationId] [int] NOT NULL,
	[Tier] [int] NOT NULL,
	[TypeId] [int] NOT NULL,
	[Tag] [nvarchar](50) NOT NULL,
	[IsPremium] [bit] NOT NULL,
	[IsJoke] [bit] NOT NULL,
	[InclusionMoment] [datetime] NOT NULL,
	[AgeDays] [int] NULL
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Historic].[Wn8Surrogate](
	[PlataformId] [int] NOT NULL,
	[TankId] [bigint] NOT NULL,
	[SurrogateTankId] [bigint] NOT NULL,
	[SecondSurrogateTankId] [bigint] NULL,
	[Reason] [nvarchar](max) NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Main].[ClanTagChange](
	[ClanId] [bigint] NOT NULL,
	[Moment] [datetime] NOT NULL,
	[PreviousClanTag] [nvarchar](5) NULL,
	[CurrentClanTag] [nvarchar](5) NULL,
 CONSTRAINT [PK_ClanTagChange] PRIMARY KEY CLUSTERED 
(
	[ClanId] ASC,
	[Moment] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Main].[DisabledReason](
	[DisabledReasonId] [int] NOT NULL,
	[Name] [varchar](15) NULL,
	[Description] [nvarchar](50) NOT NULL,
 CONSTRAINT [PL_Main_DisabledReason] PRIMARY KEY CLUSTERED 
(
	[DisabledReasonId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Main].[IgnoredPlayer](
	[PlayerId] [bigint] NOT NULL,
 CONSTRAINT [PK_IgnoredPlayer] PRIMARY KEY CLUSTERED 
(
	[PlayerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Main].[Plataform](
	[PlataformId] [int] NOT NULL,
	[Name] [varchar](7) NOT NULL,
 CONSTRAINT [PK_Plataform] PRIMARY KEY CLUSTERED 
(
	[PlataformId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UX_Plataform_name] UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Main].[PlayerGamerTagChange](
	[PlayerId] [bigint] NOT NULL,
	[Moment] [datetime] NOT NULL,
	[PreviousGamerTag] [nvarchar](25) NOT NULL,
	[CurrentGamerTag] [nvarchar](25) NOT NULL,
 CONSTRAINT [PK_PlayerGamerTagChange] PRIMARY KEY CLUSTERED 
(
	[PlayerId] ASC,
	[Moment] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Main].[Rank](
	[RankId] [int] NOT NULL,
	[Name] [varchar](30) NOT NULL,
 CONSTRAINT [PK_Rank] PRIMARY KEY CLUSTERED 
(
	[RankId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UX_Rank_Name] UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Performance].[DeltaMonthByPlayer](
	[PlayerId] [bigint] NOT NULL,
	[Date] [date] NOT NULL,
	[PreviousDate] [date] NOT NULL,
	[Moment] [datetime] NOT NULL,
	[PreviousMoment] [datetime] NOT NULL,
	[TankId] [bigint] NOT NULL,
	[LastBattle] [datetime] NOT NULL,
	[PreviousLastBattle] [datetime] NOT NULL,
	[TreesCut] [bigint] NULL,
	[MaxFrags] [bigint] NOT NULL,
	[PreviousMaxFrags] [bigint] NOT NULL,
	[MarkOfMastery] [bigint] NOT NULL,
	[PreviousMarkOfMastery] [bigint] NOT NULL,
	[BattleLifeTimeSeconds] [bigint] NULL,
	[Spotted] [bigint] NULL,
	[DamageAssistedTrack] [bigint] NULL,
	[DamageAssistedRadio] [bigint] NULL,
	[DamageAssisted] [bigint] NULL,
	[CapturePoints] [bigint] NULL,
	[DroppedCapturePoints] [bigint] NULL,
	[Battles] [bigint] NULL,
	[RecentBattles] [bigint] NOT NULL,
	[Wins] [bigint] NULL,
	[Losses] [bigint] NULL,
	[Draws] [bigint] NULL,
	[Kills] [bigint] NULL,
	[SurvivedBattles] [bigint] NULL,
	[Deaths] [bigint] NULL,
	[DamageDealt] [bigint] NULL,
	[DamageReceived] [bigint] NULL,
	[TotalDamage] [bigint] NULL,
	[PiercingsReceived] [bigint] NULL,
	[Hits] [bigint] NULL,
	[NoDamageDirectHitsReceived] [bigint] NULL,
	[ExplosionHits] [bigint] NULL,
	[Piercings] [bigint] NULL,
	[Shots] [bigint] NULL,
	[ExplosionHitsReceived] [bigint] NULL,
	[XP] [bigint] NULL,
	[DirectHitsReceived] [bigint] NULL,
	[WinRatio]  AS (([Wins]*(1.0))/[Battles]),
	[DamageDealtPerBattle]  AS (([DamageDealt]*(1.0))/[Battles]),
	[KillsPerBattle]  AS (([Kills]*(1.0))/[Battles]),
	[SpottedPerBattle]  AS (([Spotted]*(1.0))/[Battles]),
	[DroppedCapturePointsPerBattle]  AS (([DroppedCapturePoints]*(1.0))/[Battles]),
	[DamageAssistedTrackPerBattle]  AS (([DamageAssistedTrack]*(1.0))/[Battles]),
	[DamageAssistedRadioPerBattle]  AS (([DamageAssistedRadio]*(1.0))/[Battles]),
	[DamageAssistedPerBattle]  AS (([DamageAssisted]*(1.0))/[Battles]),
	[TotalDamagePerBattle]  AS (([TotalDamage]*(1.0))/[Battles]),
 CONSTRAINT [PK_Performance_DeltaMonthByPlayer] PRIMARY KEY CLUSTERED 
(
	[TankId] ASC,
	[PlayerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF, DATA_COMPRESSION = PAGE) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Performance].[MoEMethod](
	[MethodId] [int] NOT NULL,
	[Name] [varchar](15) NOT NULL,
	[Description] [nvarchar](512) NOT NULL,
 CONSTRAINT [PK_MoEMethod] PRIMARY KEY CLUSTERED 
(
	[MethodId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Performance].[MonthAgoByPlayer](
	[PlayerId] [bigint] NOT NULL,
	[Date] [date] NOT NULL,
	[Moment] [datetime] NOT NULL,
	[TankId] [bigint] NOT NULL,
	[LastBattle] [datetime] NOT NULL,
	[TreesCut] [bigint] NOT NULL,
	[MaxFrags] [bigint] NOT NULL,
	[MarkOfMastery] [bigint] NOT NULL,
	[BattleLifeTimeSeconds] [bigint] NOT NULL,
	[Spotted] [bigint] NOT NULL,
	[DamageAssistedTrack] [bigint] NOT NULL,
	[DamageAssistedRadio] [bigint] NOT NULL,
	[DamageAssisted] [bigint] NULL,
	[CapturePoints] [bigint] NOT NULL,
	[DroppedCapturePoints] [bigint] NOT NULL,
	[Battles] [bigint] NOT NULL,
	[Wins] [bigint] NOT NULL,
	[Losses] [bigint] NOT NULL,
	[Draws] [bigint] NULL,
	[WinRatio] [numeric](38, 17) NULL,
	[Kills] [bigint] NOT NULL,
	[SurvivedBattles] [bigint] NOT NULL,
	[Deaths] [bigint] NULL,
	[DamageDealt] [bigint] NOT NULL,
	[DamageReceived] [bigint] NOT NULL,
	[TotalDamage] [bigint] NULL,
	[PiercingsReceived] [bigint] NOT NULL,
	[Hits] [bigint] NOT NULL,
	[NoDamageDirectHitsReceived] [bigint] NOT NULL,
	[ExplosionHits] [bigint] NOT NULL,
	[Piercings] [bigint] NOT NULL,
	[Shots] [bigint] NOT NULL,
	[ExplosionHitsReceived] [bigint] NOT NULL,
	[XP] [bigint] NOT NULL,
	[DirectHitsReceived] [bigint] NOT NULL,
	[DateOrder] [bigint] NULL,
 CONSTRAINT [PK_Performance_MonthAgoByPlayer] PRIMARY KEY CLUSTERED 
(
	[TankId] ASC,
	[PlayerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF, DATA_COMPRESSION = PAGE) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Performance].[RecentByPlayer](
	[PlayerId] [bigint] NOT NULL,
	[Date] [date] NOT NULL,
	[Moment] [datetime] NOT NULL,
	[TankId] [bigint] NOT NULL,
	[LastBattle] [datetime] NOT NULL,
	[TreesCut] [bigint] NOT NULL,
	[MaxFrags] [bigint] NOT NULL,
	[MarkOfMastery] [bigint] NOT NULL,
	[BattleLifeTimeSeconds] [bigint] NOT NULL,
	[Spotted] [bigint] NOT NULL,
	[DamageAssistedTrack] [bigint] NOT NULL,
	[DamageAssistedRadio] [bigint] NOT NULL,
	[DamageAssisted] [bigint] NULL,
	[CapturePoints] [bigint] NOT NULL,
	[DroppedCapturePoints] [bigint] NOT NULL,
	[Battles] [bigint] NOT NULL,
	[Wins] [bigint] NOT NULL,
	[Losses] [bigint] NOT NULL,
	[Draws] [bigint] NULL,
	[WinRatio] [numeric](38, 17) NULL,
	[Kills] [bigint] NOT NULL,
	[SurvivedBattles] [bigint] NOT NULL,
	[Deaths] [bigint] NULL,
	[DamageDealt] [bigint] NOT NULL,
	[DamageReceived] [bigint] NOT NULL,
	[TotalDamage] [bigint] NULL,
	[PiercingsReceived] [bigint] NOT NULL,
	[Hits] [bigint] NOT NULL,
	[NoDamageDirectHitsReceived] [bigint] NOT NULL,
	[ExplosionHits] [bigint] NOT NULL,
	[Piercings] [bigint] NOT NULL,
	[Shots] [bigint] NOT NULL,
	[ExplosionHitsReceived] [bigint] NOT NULL,
	[XP] [bigint] NOT NULL,
	[DirectHitsReceived] [bigint] NOT NULL,
	[DateOrder] [bigint] NULL,
	[DamageDealtPerBattle]  AS (([DamageDealt]*(1.0))/[Battles]),
	[KillsPerBattle]  AS (([Kills]*(1.0))/[Battles]),
	[SpottedPerBattle]  AS (([Spotted]*(1.0))/[Battles]),
	[DroppedCapturePointsPerBattle]  AS (([DroppedCapturePoints]*(1.0))/[Battles]),
	[DamageAssistedTrackPerBattle]  AS (([DamageAssistedTrack]*(1.0))/[Battles]),
	[DamageAssistedRadioPerBattle]  AS (([DamageAssistedRadio]*(1.0))/[Battles]),
	[DamageAssistedPerBattle]  AS (([DamageAssisted]*(1.0))/[Battles]),
	[TotalDamagePerBattle]  AS (([TotalDamage]*(1.0))/[Battles]),
	[TreesCutPerBattle]  AS (([TreesCut]*(1.0))/[Battles]),
 CONSTRAINT [PK_Performance_RecentByPlayer] PRIMARY KEY CLUSTERED 
(
	[TankId] ASC,
	[PlayerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF, DATA_COMPRESSION = PAGE) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Performance].[RecentTotalsByPlayer](
	[Date] [date] NULL,
	[PlayerId] [bigint] NOT NULL,
	[Battles] [bigint] NULL,
	[BattleLifeTimeSeconds] [bigint] NULL,
	[XP] [bigint] NULL,
	[Wins] [bigint] NULL,
 CONSTRAINT [PK_Performance_RecentTotalsByPlayer] PRIMARY KEY CLUSTERED 
(
	[PlayerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF, DATA_COMPRESSION = PAGE) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Store].[Clan](
	[ClanId] [bigint] NOT NULL,
	[Moment] [datetime] NOT NULL,
	[BinData] [varbinary](max) NOT NULL,
 CONSTRAINT [PK_Store_Clan] PRIMARY KEY CLUSTERED 
(
	[ClanId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Store].[General](
	[Key] [varchar](15) NOT NULL,
	[Moment] [datetime] NOT NULL,
	[Data] [nvarchar](max) NULL,
 CONSTRAINT [PK_Store_General] PRIMARY KEY CLUSTERED 
(
	[Key] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Store].[Player](
	[PlayerId] [bigint] NOT NULL,
	[Moment] [datetime] NOT NULL,
	[BinData] [varbinary](max) NOT NULL,
 CONSTRAINT [PK_Store_Player] PRIMARY KEY CLUSTERED 
(
	[PlayerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Tanks].[Wn8PcExpected](
	[Date] [date] NOT NULL,
	[Source] [varchar](15) NOT NULL,
	[Version] [varchar](15) NOT NULL,
	[TankId] [bigint] NOT NULL,
	[Def] [float] NOT NULL,
	[Frag] [float] NOT NULL,
	[Spot] [float] NOT NULL,
	[Damage] [float] NOT NULL,
	[WinRate] [float] NOT NULL,
 CONSTRAINT [PK_Performance_Wn8PcExpected] PRIMARY KEY CLUSTERED 
(
	[Date] ASC,
	[Source] ASC,
	[Version] ASC,
	[TankId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [Tanks].[Wn8XvmExclusion](
	[TankId] [bigint] NOT NULL,
	[Reason] [nvarchar](max) NOT NULL,
 CONSTRAINT [PK_Tanks_Wn8SourceExclusion] PRIMARY KEY CLUSTERED 
(
	[TankId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_PlayerMedal_MedalCode] ON [Achievements].[PlayerMedal]
(
	[MedalCode] ASC
)
INCLUDE([Count],[Battles]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Current_ClanPlayer_PlayerId] ON [Current].[ClanPlayer]
(
	[PlayerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Current_Player_MonthBattles] ON [Current].[Player]
(
	[MonthBattles] ASC
)
INCLUDE([PlayerId]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_Main_Clan_ClanTag] ON [Main].[Clan]
(
	[ClanTag] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_ClanDatePlayer_Date_PlayerId] ON [Main].[ClanDatePlayer]
(
	[Date] ASC,
	[PlayerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF, DATA_COMPRESSION = PAGE) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_Player_GamerTagPlataformId] ON [Main].[Player]
(
	[GamerTag] ASC,
	[PlataformId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Performance_by_player] ON [Performance].[PlayerDate]
(
	[PlayerId] ASC,
	[TankId] ASC,
	[Date] ASC
)
INCLUDE([Battles],[DamageDealt],[Wins],[Kills],[Spotted],[DroppedCapturePoints]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Performance_ShouldUpdate] ON [Performance].[PlayerDate]
(
	[PlayerId] ASC,
	[TankId] ASC,
	[Date] ASC
)
INCLUDE([LastBattle],[Battles],[XP]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [Achievements].[Medal] ADD  CONSTRAINT [DF_Medal_Inclusion]  DEFAULT (getutcdate()) FOR [InclusionMoment]
GO
ALTER TABLE [Achievements].[PlayerMedal] ADD  CONSTRAINT [DF_PlayerMedal_Battles]  DEFAULT ((1)) FOR [Battles]
GO
ALTER TABLE [Current].[Clan] ADD  CONSTRAINT [DF_CurrentClan_Top15AvgTier]  DEFAULT ((10)) FOR [Top15AvgTier]
GO
ALTER TABLE [Current].[Clan] ADD  CONSTRAINT [DF_CurrentClan_ActiveAvgTier]  DEFAULT ((10)) FOR [ActiveAvgTier]
GO
ALTER TABLE [Current].[Clan] ADD  CONSTRAINT [DF_CurrentClan_TotalAvgTier]  DEFAULT ((10)) FOR [TotalAvgTier]
GO
ALTER TABLE [Current].[Player] ADD  CONSTRAINT [DF_Current_Player_IsPatched]  DEFAULT ((0)) FOR [IsPatched]
GO
ALTER TABLE [Current].[Player] ADD  CONSTRAINT [DF_Current_Player_Origin]  DEFAULT ((0)) FOR [Origin]
GO
ALTER TABLE [Current].[Player] ADD  CONSTRAINT [DF_Current_Player_TotalTier]  DEFAULT ((10.0)) FOR [TotalTier]
GO
ALTER TABLE [Current].[Player] ADD  CONSTRAINT [DF_Current_Player_MonthTier]  DEFAULT ((10.0)) FOR [MonthTier]
GO
ALTER TABLE [Current].[Player] ADD  CONSTRAINT [DF_Curr_Player_Tier10TotalBattles]  DEFAULT ((0)) FOR [Tier10TotalBattles]
GO
ALTER TABLE [Current].[Player] ADD  CONSTRAINT [DF_Curr_Player_Tier10MonthBattles]  DEFAULT ((0)) FOR [Tier10MonthBattles]
GO
ALTER TABLE [Current].[Player] ADD  CONSTRAINT [DF_Curr_Player_Tier10TotalWinRate]  DEFAULT ((0.0)) FOR [Tier10TotalWinRate]
GO
ALTER TABLE [Current].[Player] ADD  CONSTRAINT [DF_Curr_Player_Tier10MonthWinRate]  DEFAULT ((0.0)) FOR [Tier10MonthWinRate]
GO
ALTER TABLE [Current].[Player] ADD  CONSTRAINT [DF_Curr_Player_Tier10TotalWN8]  DEFAULT ((0.0)) FOR [Tier10TotalWN8]
GO
ALTER TABLE [Current].[Player] ADD  CONSTRAINT [DF_Curr_Player_Tier10MonthWN8]  DEFAULT ((0.0)) FOR [Tier10MonthWN8]
GO
ALTER TABLE [Main].[Clan] ADD  CONSTRAINT [DF_Clan_Delay]  DEFAULT ((1.0)) FOR [Delay]
GO
ALTER TABLE [Main].[Clan] ADD  CONSTRAINT [DF_Clan_IsHidden]  DEFAULT ((0)) FOR [IsHidden]
GO
ALTER TABLE [Main].[Clan] ADD  CONSTRAINT [DF_Clan_DisabledReason]  DEFAULT ((0)) FOR [DisabledReason]
GO
ALTER TABLE [Main].[Clan] ADD  CONSTRAINT [DF_Clan_Inclusion]  DEFAULT (getutcdate()) FOR [InclusionMoment]
GO
ALTER TABLE [Main].[ClanDate] ADD  CONSTRAINT [DF_MainClanDate_Top15AvgTier]  DEFAULT ((10)) FOR [Top15AvgTier]
GO
ALTER TABLE [Main].[ClanDate] ADD  CONSTRAINT [DF_MainClanDate_ActiveAvgTier]  DEFAULT ((10)) FOR [ActiveAvgTier]
GO
ALTER TABLE [Main].[ClanDate] ADD  CONSTRAINT [DF_MainClanDate_TotalAvgTier]  DEFAULT ((10)) FOR [TotalAvgTier]
GO
ALTER TABLE [Main].[Player] ADD  CONSTRAINT [DF_Player_Delay]  DEFAULT ((1.0)) FOR [Delay]
GO
ALTER TABLE [Main].[Player] ADD  CONSTRAINT [DF_Player_Inclusion]  DEFAULT (getutcdate()) FOR [InclusionMoment]
GO
ALTER TABLE [Main].[PlayerDate] ADD  CONSTRAINT [DF_PlayerDate_IsPatched]  DEFAULT ((0)) FOR [IsPatched]
GO
ALTER TABLE [Main].[PlayerDate] ADD  CONSTRAINT [DF_PlayerDate_Origin]  DEFAULT ((0)) FOR [Origin]
GO
ALTER TABLE [Main].[PlayerDate] ADD  CONSTRAINT [DF_Main_PlayerDate_TotalTier]  DEFAULT ((10.0)) FOR [TotalTier]
GO
ALTER TABLE [Main].[PlayerDate] ADD  CONSTRAINT [DF_Main_PlayerDate_MonthTier]  DEFAULT ((10.0)) FOR [MonthTier]
GO
ALTER TABLE [Main].[PlayerDate] ADD  CONSTRAINT [DF_Curr_Player_Tier10TotalBattles]  DEFAULT ((0)) FOR [Tier10TotalBattles]
GO
ALTER TABLE [Main].[PlayerDate] ADD  CONSTRAINT [DF_Curr_Player_Tier10MonthBattles]  DEFAULT ((0)) FOR [Tier10MonthBattles]
GO
ALTER TABLE [Main].[PlayerDate] ADD  CONSTRAINT [DF_Curr_Player_Tier10TotalWinRate]  DEFAULT ((0.0)) FOR [Tier10TotalWinRate]
GO
ALTER TABLE [Main].[PlayerDate] ADD  CONSTRAINT [DF_Curr_Player_Tier10MonthWinRate]  DEFAULT ((0.0)) FOR [Tier10MonthWinRate]
GO
ALTER TABLE [Main].[PlayerDate] ADD  CONSTRAINT [DF_Curr_Player_Tier10TotalWN8]  DEFAULT ((0.0)) FOR [Tier10TotalWN8]
GO
ALTER TABLE [Main].[PlayerDate] ADD  CONSTRAINT [DF_Curr_Player_Tier10MonthWN8]  DEFAULT ((0.0)) FOR [Tier10MonthWN8]
GO
ALTER TABLE [Performance].[PlayerDate] ADD  DEFAULT ((0)) FOR [PiercingsReceived]
GO
ALTER TABLE [Performance].[PlayerDate] ADD  DEFAULT ((0)) FOR [Hits]
GO
ALTER TABLE [Performance].[PlayerDate] ADD  DEFAULT ((0)) FOR [NoDamageDirectHitsReceived]
GO
ALTER TABLE [Performance].[PlayerDate] ADD  DEFAULT ((0)) FOR [ExplosionHits]
GO
ALTER TABLE [Performance].[PlayerDate] ADD  DEFAULT ((0)) FOR [Piercings]
GO
ALTER TABLE [Performance].[PlayerDate] ADD  DEFAULT ((0)) FOR [Shots]
GO
ALTER TABLE [Performance].[PlayerDate] ADD  DEFAULT ((0)) FOR [ExplosionHitsReceived]
GO
ALTER TABLE [Performance].[PlayerDate] ADD  DEFAULT ((0)) FOR [XP]
GO
ALTER TABLE [Performance].[PlayerDate] ADD  DEFAULT ((0)) FOR [DirectHitsReceived]
GO
ALTER TABLE [Performance].[PlayerDate] ADD  CONSTRAINT [DF_Performance_PlayerDate_Anchor]  DEFAULT ((0)) FOR [IsAnchor]
GO
ALTER TABLE [Performance].[ReferenceValues] ADD  DEFAULT ((0)) FOR [TreesCut]
GO
ALTER TABLE [Performance].[ReferenceValues] ADD  DEFAULT ((0)) FOR [MaxFrags]
GO
ALTER TABLE [Performance].[ReferenceValues] ADD  DEFAULT ((0)) FOR [MarkOfMastery]
GO
ALTER TABLE [Performance].[ReferenceValues] ADD  DEFAULT ((0)) FOR [Hits]
GO
ALTER TABLE [Performance].[ReferenceValues] ADD  DEFAULT ((0)) FOR [NoDamageDirectHitsReceived]
GO
ALTER TABLE [Performance].[ReferenceValues] ADD  DEFAULT ((0)) FOR [ExplosionHits]
GO
ALTER TABLE [Performance].[ReferenceValues] ADD  DEFAULT ((0)) FOR [Piercings]
GO
ALTER TABLE [Performance].[ReferenceValues] ADD  DEFAULT ((0)) FOR [Shots]
GO
ALTER TABLE [Performance].[ReferenceValues] ADD  DEFAULT ((0)) FOR [ExplosionHitsReceived]
GO
ALTER TABLE [Performance].[ReferenceValues] ADD  DEFAULT ((0)) FOR [XP]
GO
ALTER TABLE [Performance].[ReferenceValues] ADD  DEFAULT ((0)) FOR [DirectHitsReceived]
GO
ALTER TABLE [Performance].[ReferenceValues] ADD  DEFAULT ((0)) FOR [PiercingsReceived]
GO
ALTER TABLE [Tanks].[PcTank] ADD  CONSTRAINT [df_pctank_isjoke]  DEFAULT ((0)) FOR [IsJoke]
GO
ALTER TABLE [Tanks].[PcTank] ADD  CONSTRAINT [DF_PcTank_Inclusion]  DEFAULT (getutcdate()) FOR [InclusionMoment]
GO
ALTER TABLE [Tanks].[Tank] ADD  CONSTRAINT [df_tank_isjoke]  DEFAULT ((0)) FOR [IsJoke]
GO
ALTER TABLE [Tanks].[Tank] ADD  CONSTRAINT [DF_Tank_Inclusion]  DEFAULT (getutcdate()) FOR [InclusionMoment]
GO
ALTER TABLE [Tanks].[Wn8Expected] ADD  CONSTRAINT [DF_Wn8Expected_Origin]  DEFAULT ((0)) FOR [Origin]
GO
ALTER TABLE [Achievements].[PlayerMedal]  WITH NOCHECK ADD  CONSTRAINT [FK_Achievements_PlayerMedal_Medal] FOREIGN KEY([MedalCode])
REFERENCES [Achievements].[Medal] ([MedalCode])
GO
ALTER TABLE [Achievements].[PlayerMedal] CHECK CONSTRAINT [FK_Achievements_PlayerMedal_Medal]
GO
ALTER TABLE [Achievements].[PlayerMedal]  WITH NOCHECK ADD  CONSTRAINT [FK_Achievements_PlayerMedal_Player] FOREIGN KEY([PlayerId])
REFERENCES [Main].[Player] ([PlayerId])
ON DELETE CASCADE
GO
ALTER TABLE [Achievements].[PlayerMedal] CHECK CONSTRAINT [FK_Achievements_PlayerMedal_Player]
GO
ALTER TABLE [Achievements].[PlayerMedal]  WITH NOCHECK ADD  CONSTRAINT [FK_PlayerMedal_Tank] FOREIGN KEY([TankId])
REFERENCES [Tanks].[Tank] ([TankId])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [Achievements].[PlayerMedal] CHECK CONSTRAINT [FK_PlayerMedal_Tank]
GO
ALTER TABLE [Current].[Clan]  WITH CHECK ADD  CONSTRAINT [FK_Current_Clan_Main_Clan] FOREIGN KEY([ClanId])
REFERENCES [Main].[Clan] ([ClanId])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [Current].[Clan] CHECK CONSTRAINT [FK_Current_Clan_Main_Clan]
GO
ALTER TABLE [Current].[ClanPlayer]  WITH CHECK ADD  CONSTRAINT [FK_Current_ClanPlayer_Current_Clan] FOREIGN KEY([ClanId])
REFERENCES [Current].[Clan] ([ClanId])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [Current].[ClanPlayer] CHECK CONSTRAINT [FK_Current_ClanPlayer_Current_Clan]
GO
ALTER TABLE [Current].[ClanPlayer]  WITH CHECK ADD  CONSTRAINT [FK_Current_ClanPlayer_Player] FOREIGN KEY([PlayerId])
REFERENCES [Main].[Player] ([PlayerId])
GO
ALTER TABLE [Current].[ClanPlayer] CHECK CONSTRAINT [FK_Current_ClanPlayer_Player]
GO
ALTER TABLE [Current].[ClanPlayer]  WITH CHECK ADD  CONSTRAINT [FK_Current_ClanPlayer_Rank] FOREIGN KEY([RankId])
REFERENCES [Main].[Rank] ([RankId])
GO
ALTER TABLE [Current].[ClanPlayer] CHECK CONSTRAINT [FK_Current_ClanPlayer_Rank]
GO
ALTER TABLE [Current].[Player]  WITH CHECK ADD  CONSTRAINT [FK_Current_Player_Player] FOREIGN KEY([PlayerId])
REFERENCES [Main].[Player] ([PlayerId])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [Current].[Player] CHECK CONSTRAINT [FK_Current_Player_Player]
GO
ALTER TABLE [Discord].[UserPlayer]  WITH CHECK ADD  CONSTRAINT [FK_UserPlayer_Player] FOREIGN KEY([PlayerId])
REFERENCES [Main].[Player] ([PlayerId])
ON DELETE CASCADE
GO
ALTER TABLE [Discord].[UserPlayer] CHECK CONSTRAINT [FK_UserPlayer_Player]
GO
ALTER TABLE [Main].[Clan]  WITH CHECK ADD  CONSTRAINT [FK_Clan_DisabledReason] FOREIGN KEY([DisabledReason])
REFERENCES [Main].[DisabledReason] ([DisabledReasonId])
GO
ALTER TABLE [Main].[Clan] CHECK CONSTRAINT [FK_Clan_DisabledReason]
GO
ALTER TABLE [Main].[ClanDate]  WITH CHECK ADD  CONSTRAINT [FK_Main_ClanDate_Main_Clan] FOREIGN KEY([ClanId])
REFERENCES [Main].[Clan] ([ClanId])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [Main].[ClanDate] CHECK CONSTRAINT [FK_Main_ClanDate_Main_Clan]
GO
ALTER TABLE [Main].[ClanDatePlayer]  WITH CHECK ADD  CONSTRAINT [FK_ClanDatePlayer_ClanDate] FOREIGN KEY([ClanId], [Date])
REFERENCES [Main].[ClanDate] ([ClanId], [Date])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [Main].[ClanDatePlayer] CHECK CONSTRAINT [FK_ClanDatePlayer_ClanDate]
GO
ALTER TABLE [Main].[ClanDatePlayer]  WITH CHECK ADD  CONSTRAINT [FK_ClanDatePlayer_Player] FOREIGN KEY([PlayerId])
REFERENCES [Main].[Player] ([PlayerId])
GO
ALTER TABLE [Main].[ClanDatePlayer] CHECK CONSTRAINT [FK_ClanDatePlayer_Player]
GO
ALTER TABLE [Main].[ClanDatePlayer]  WITH CHECK ADD  CONSTRAINT [FK_Main_ClanDatePlayer_Rank] FOREIGN KEY([RankId])
REFERENCES [Main].[Rank] ([RankId])
GO
ALTER TABLE [Main].[ClanDatePlayer] CHECK CONSTRAINT [FK_Main_ClanDatePlayer_Rank]
GO
ALTER TABLE [Main].[Player]  WITH CHECK ADD  CONSTRAINT [FK_Player_Plataform] FOREIGN KEY([PlataformId])
REFERENCES [Main].[Plataform] ([PlataformId])
GO
ALTER TABLE [Main].[Player] CHECK CONSTRAINT [FK_Player_Plataform]
GO
ALTER TABLE [Main].[PlayerDate]  WITH CHECK ADD  CONSTRAINT [FK_PlayerDate_Player] FOREIGN KEY([PlayerId])
REFERENCES [Main].[Player] ([PlayerId])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [Main].[PlayerDate] CHECK CONSTRAINT [FK_PlayerDate_Player]
GO
ALTER TABLE [Main].[PlayerGamerTagChange]  WITH CHECK ADD  CONSTRAINT [FK_PlayerGamerTagChange_Player] FOREIGN KEY([PlayerId])
REFERENCES [Main].[Player] ([PlayerId])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [Main].[PlayerGamerTagChange] CHECK CONSTRAINT [FK_PlayerGamerTagChange_Player]
GO
ALTER TABLE [Performance].[MoE]  WITH CHECK ADD  CONSTRAINT [FK_Moe_Method] FOREIGN KEY([MethodId])
REFERENCES [Performance].[MoEMethod] ([MethodId])
GO
ALTER TABLE [Performance].[MoE] CHECK CONSTRAINT [FK_Moe_Method]
GO
ALTER TABLE [Performance].[PlayerDate]  WITH CHECK ADD  CONSTRAINT [FK_PerfPlayerDate_Player] FOREIGN KEY([PlayerId])
REFERENCES [Main].[Player] ([PlayerId])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [Performance].[PlayerDate] CHECK CONSTRAINT [FK_PerfPlayerDate_Player]
GO
ALTER TABLE [Performance].[ReferenceValues]  WITH CHECK ADD  CONSTRAINT [FK_ReferenceValues_Tank] FOREIGN KEY([TankId])
REFERENCES [Tanks].[Tank] ([TankId])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [Performance].[ReferenceValues] CHECK CONSTRAINT [FK_ReferenceValues_Tank]
GO
ALTER TABLE [Tanks].[PcTank]  WITH CHECK ADD  CONSTRAINT [FK_PcTank_Nation] FOREIGN KEY([NationId])
REFERENCES [Tanks].[Nation] ([NationId])
GO
ALTER TABLE [Tanks].[PcTank] CHECK CONSTRAINT [FK_PcTank_Nation]
GO
ALTER TABLE [Tanks].[PcTank]  WITH CHECK ADD  CONSTRAINT [FK_PcTank_Type] FOREIGN KEY([TypeId])
REFERENCES [Tanks].[Type] ([TypeId])
GO
ALTER TABLE [Tanks].[PcTank] CHECK CONSTRAINT [FK_PcTank_Type]
GO
ALTER TABLE [Tanks].[Tank]  WITH CHECK ADD  CONSTRAINT [FK_Tank_Nation] FOREIGN KEY([NationId])
REFERENCES [Tanks].[Nation] ([NationId])
GO
ALTER TABLE [Tanks].[Tank] CHECK CONSTRAINT [FK_Tank_Nation]
GO
ALTER TABLE [Tanks].[Tank]  WITH CHECK ADD  CONSTRAINT [FK_Tank_Type] FOREIGN KEY([TypeId])
REFERENCES [Tanks].[Type] ([TypeId])
GO
ALTER TABLE [Tanks].[Tank] CHECK CONSTRAINT [FK_Tank_Type]
GO
ALTER TABLE [Tanks].[Wn8Expected]  WITH CHECK ADD  CONSTRAINT [FK_Wn8Expected_Tank] FOREIGN KEY([TankId])
REFERENCES [Tanks].[Tank] ([TankId])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [Tanks].[Wn8Expected] CHECK CONSTRAINT [FK_Wn8Expected_Tank]
GO
ALTER TABLE [Tanks].[Wn8Expected]  WITH CHECK ADD  CONSTRAINT [FK_Wn8Expected_Wn8ExpectedLoad] FOREIGN KEY([Date], [Source], [Version])
REFERENCES [Tanks].[Wn8ExpectedLoad] ([Date], [Source], [Version])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [Tanks].[Wn8Expected] CHECK CONSTRAINT [FK_Wn8Expected_Wn8ExpectedLoad]
GO
ALTER TABLE [Tanks].[Wn8PcExpected]  WITH CHECK ADD  CONSTRAINT [FK_WnPcExpected_PcTank] FOREIGN KEY([TankId])
REFERENCES [Tanks].[PcTank] ([TankId])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [Tanks].[Wn8PcExpected] CHECK CONSTRAINT [FK_WnPcExpected_PcTank]
GO
ALTER TABLE [Tanks].[Wn8PcExpected]  WITH CHECK ADD  CONSTRAINT [FK_WnPcExpected_Wn8ExpectedLoad] FOREIGN KEY([Date], [Source], [Version])
REFERENCES [Tanks].[Wn8ExpectedLoad] ([Date], [Source], [Version])
ON UPDATE CASCADE
ON DELETE CASCADE
GO
ALTER TABLE [Tanks].[Wn8PcExpected] CHECK CONSTRAINT [FK_WnPcExpected_Wn8ExpectedLoad]
GO
ALTER TABLE [Tanks].[Wn8XvmSurrogate]  WITH CHECK ADD  CONSTRAINT [FK_Tanks_Wn8Surrogate_SecondSurrogateTank] FOREIGN KEY([SecondSurrogateTankId])
REFERENCES [Tanks].[PcTank] ([TankId])
GO
ALTER TABLE [Tanks].[Wn8XvmSurrogate] CHECK CONSTRAINT [FK_Tanks_Wn8Surrogate_SecondSurrogateTank]
GO
ALTER TABLE [Tanks].[Wn8XvmSurrogate]  WITH CHECK ADD  CONSTRAINT [FK_Tanks_Wn8Surrogate_SurrogateTank] FOREIGN KEY([SurrogateTankId])
REFERENCES [Tanks].[PcTank] ([TankId])
GO
ALTER TABLE [Tanks].[Wn8XvmSurrogate] CHECK CONSTRAINT [FK_Tanks_Wn8Surrogate_SurrogateTank]
GO
ALTER TABLE [Tanks].[Wn8XvmSurrogate]  WITH CHECK ADD  CONSTRAINT [FK_Tanks_Wn8Surrogate_Tank] FOREIGN KEY([TankId])
REFERENCES [Tanks].[Tank] ([TankId])
GO
ALTER TABLE [Tanks].[Wn8XvmSurrogate] CHECK CONSTRAINT [FK_Tanks_Wn8Surrogate_Tank]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE proc [Achievements].[SetMedal]
(
	@MedalCode varchar(25),
	@Name nvarchar(50),
	@Description nvarchar(700),
	@HeroInformation nvarchar(700),
	@Condition nvarchar(700),
	@CategoryId int,
	@TypeId int,
	@SectionId int
)
as
begin

	set nocount on;

	begin tran Achievements_SetMedal;

	update Achievements.Medal 
		set 
			[Name] = @Name,
			[Description] = @Description,
			HeroInformation = @HeroInformation,
			Condition = @Condition,
			CategoryId = @CategoryId,
			TypeId = @TypeId,
			SectionId = @SectionId
		where
			(MedalCode = @MedalCode);

	if (@@ROWCOUNT <= 0) 
	begin
		insert into Achievements.Medal (MedalCode, [Name], [Description], HeroInformation, Condition, CategoryId, TypeId, SectionId)
			values (@MedalCode, @Name, @Description, @HeroInformation, @Condition, @CategoryId, @TypeId, @SectionId);			
	end;

	commit tran Achievements_SetMedal;

end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE proc [Achievements].[SetPlayerMedal]
(
	@PlayerId bigint,
	@TankId bigint,
	@MedalCode varchar(25),
	@Count int
)
as
begin
	set nocount on;

	begin tran SetPlayerMedal;

	update Achievements.PlayerMedal
		set
			[Count] = @Count
		where
			(PlayerId = @PlayerId) and (TankId = @TankId) and (MedalCode = @MedalCode);

	if (@@ROWCOUNT <= 0)
	begin
		insert into Achievements.PlayerMedal (PlayerId, TankId, MedalCode, [Count]) 
			values (@PlayerId, @TankId, @MedalCode, @Count);
	end;
		
	commit tran SetPlayerMedal;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE proc [Discord].[AssociateUserToPlayer]
(
	@userId bigint,
	@playerId bigint
)
as
begin
	set nocount on;

	begin tran Discord_AssociateUserToPlayer;

	if (@playerId = 0)
	begin
		-- whants to delete the association
		delete from Discord.UserPlayer where UserId = @userId;
		commit tran Discord_AssociateUserToPlayer;
		return;
	end;

	if (exists(select 1 from Discord.UserPlayer where UserId = @userId))
	begin
		update Discord.UserPlayer set PlayerId = @playerId where UserId = @userId;
	end else
	begin
		insert into Discord.UserPlayer (UserId, PlayerId) values (@userId, @playerId);	
	end;

	commit tran Discord_AssociateUserToPlayer;

end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE proc [Main].[AddClan]
(
	@clanId bigint,
	@clanTag varchar(5),
	@country char(2)
)
as
begin
	set nocount on;

	begin tran AddClan;

	if exists (select 1 from Main.Clan where (ClanTag = @clanTag) and (ClanId <> @clanId) and ([Enabled] = 0))
	begin
		-- Estou adicionando um novo cla, mas existe um velho com a mesma tag
		-- Troco a tag do velho

		declare @newTag varchar(5);
		set @newTag = ltrim(rtrim('+' + left(@clanTag, 4)));

		update Main.Clan 
			set ClanTag = @newTag
			where (ClanTag = @clanTag) and (ClanId <> @clanId) and ([Enabled] = 0);

	end;

	if exists (select 1 from Main.Clan where (ClanId = @clanId)) 
	begin
		-- já existe, apenas seta os dados e habilita
		update Main.Clan
			set 
				ClanTag = @clanTag,
				FlagCode = @country, 
				[Enabled] = 1, [Delay] = 1, DisabledReason = 0
			where (ClanId = @clanId);		
	end
	else begin
		-- insere o novo cla
		insert into Main.Clan (ClanTag, [Name], FlagCode, [Enabled], [Delay], ClanId)
			values (@clanTag, '', @country, 1, 1, @clanId);
	end;

	commit tran AddClan;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


-- Balança o schedule
CREATE proc [Main].[BalanceSchedule]
(
    @minPlayers int, --  Mínimo de Jogadores por Dia
    @maxPlayers int,  -- Máximo de Jogadores por Dia
	@saveChanges bit = 1, -- if the changes should be saved
	@debug bit = 0 -- to dump intermediary tables
)
as
begin
    set nocount on;
		
    -- Desabilita Clãs Inativos
    update Main.Clan
	   set [Enabled] = 0, DisabledReason = 1
	   from Main.Clan c
		  inner join (
			 select cd.ClanId, 
				    MAX(cd.ActiveMembers) as MaxActive, Min(cd.ActiveMembers) as MinActive, AVG(cd.ActiveMembers) as AvgActive,
				    COUNT(*) as InnactiveCount, MAX(cd.ActiveMembers*1.0/cd.TotalMembers) as MaxActivity
				from Main.ClanDate cd
				inner join Main.Clan c
				    on (c.ClanId = cd.ClanId)
				where (c.[Enabled] = 1) and (cd.ActiveMembers is not null)
				    and ((cd.ActiveMembers < 4) -- Menos de 4 ativos
						or ((cd.ActiveMembers*1.0/cd.TotalMembers) < 0.25)) -- Menos de 25% do Clã Ativo
				    and (DATEDIFF(MONTH, cd.[Date], GETUTCDATE()) < 1) -- Só dados do Ultimo Mês na conta
				group by cd.ClanId
		  ) i
		  on (i.ClanId = c.ClanId)
	   where ((i.MaxActive < 4) -- Menos de 4 ativos
			  or (i.MaxActivity < 0.25)) -- Menos de 25% do Clã Ativo
		  and (i.InnactiveCount > 28); -- 28 dias do ultimo mês com menos de 4 ativos;

	-- tira os desabilidados da tabela de correntes
	delete [Current].Clan
		--select *
		from [Current].Clan cc
			inner join Main.Clan c 
				on (c.ClanId = cc.ClanId)
		where c.[Enabled] = 0;

    declare @currentPlayers int;
    select @currentPlayers = PlayersPerDay from Main.NumberOfPlayersPerDay;
    print 'Jogadores Correntes: ' + cast(@currentPlayers as varchar(10));

    if ((@minPlayers <= @currentPlayers) and (@currentPlayers <= @maxPlayers))
    begin
	   print 'Sem necessidade de balancear';
	   return;
    end;

	IF OBJECT_ID('tempdb..#Innactive') IS NOT NULL 
    begin
	   drop table #Innactive;
    end;

	select 		
		ClanId, avg(TotalDelay) as AvgInnactiveTotalDelay, COUNT(1) as Innactives
		into #Innactive
		from [Main].[RecentClanCompositionStats]
		where [Enabled] = 1 and IsActive = 0
		group by ClanId;

    IF OBJECT_ID('tempdb..#Balance') IS NOT NULL 
    begin
	   drop table #Balance;
    end;

	select  
			rcd.ClanId, c.[Delay], 
			isnull(rcd.TotalMembers, rcdc.TotalMembers) as TotalMembers, 
			isnull(rcd.ActiveMembers, rcdc.ActiveMembers) as ActiveMembers, 
			isnull(rcd.WN8t15, rcdc.WN8t15) as WN8t15, 
			0 as Balanced, 
			(isnull(rcd.TotalMembers, rcdc.TotalMembers) - isnull(rcd.ActiveMembers, rcdc.ActiveMembers)) as InactiveMembers, 
			c.IsHidden, rcd.[Date], rcdc.[Date] as LastCalculationDate, c.AgeDays, isnull(i.AvgInnactiveTotalDelay, 6.0) as AvgInnactiveTotalDelay,
			c.ClanTag
		into #Balance
		from Main.RecentClanDate rcd
			inner join [Main].[RecentClanDateCalculation] rcdc
				on (rcd.ClanId = rcdc.ClanId)
			inner join Main.Clan c
				on (c.ClanId = rcd.ClanId)
			left outer join #Innactive i
				on (c.ClanId = i.ClanId)
		where rcd.[Enabled] = 1
		order by TotalMembers;

    alter table #Balance add EffectiveMembers as ((ActiveMembers * (1.0/[Delay])) + (InactiveMembers * (1.0/AvgInnactiveTotalDelay)));
		
    -- Queries para ver o estado do balanço proposto    
    --select sum(EffectiveMembers) as ProposedPerDay, (24*60*2 - 30) as IdealPerDay from #Balance;

    if exists (select 1 from #Balance where WN8t15 is null)
    begin
	   declare @notCalculatedCount int;
	   set @notCalculatedCount = (select count(*) from #Balance where WN8t15 is null)	   

	   select * from #Balance where WN8t15 is null order by ClanId;

	   drop table #Balance;
	   print 'Há ' + cast(@notCalculatedCount as varchar(10)) + ' clãs ativos ainda não pegos na data de hoje.'
	   return;
    end;
	
    -- Selva é prioritario, pois é meu clã
    update #Balance set [Delay] = 1, Balanced = 1 where ClanId = 208;

	-- Com menos de 15 dias, é sempre 1
	update #Balance set [Delay] = 1, Balanced = 1 where Balanced = 0 and AgeDays <= 14;

    -- Menos de 7 totais
    update #Balance set [Delay] = 4, Balanced = 1 where Balanced = 0 and TotalMembers < 7;

    -- Menos de 7 ativos
    update #Balance set [Delay] = 3, Balanced = 1 where Balanced = 0 and ActiveMembers < 7;

	-- WN8t15 menos que 450 (Bellow Average)
    update #Balance set [Delay] = 2, Balanced = 1 where Balanced = 0 and WN8t15 < 450;

    -- WN8t15 maior que 2450 (Unicum) é Delay = 1
    update #Balance set [Delay] = 1, Balanced = 1 where Balanced = 0 and WN8t15 >= 2450;
	
	-- Invisiveis são sempre atrasados, mesmo sendo bons
    update #Balance set [Delay] = 3, Balanced = 1 where IsHidden = 1;

    -- Seta todos os não balanceados ainda para Delay = 2, transientemente
    update #Balance set [Delay] = 2 where Balanced = 0;

	-- Queries para ver o estado do balanço proposto
	if (@debug = 1)
	begin
		select * from #Balance order by [Delay] desc, ClanTag;
	end;

	--return; -- Abort Prematuro

    -- BEGIN Parte iterativa: Baseado no WN8t15, vem colocando Delay = 1 para os melhores até chegar na coleta ideal
    declare @clanId bigint;
    
    declare @iter int;
    set @iter = 0;

    declare @proposed int;
    select @proposed = sum(EffectiveMembers) from #Balance;

	declare @continue bit
	set @continue = 1;

    print 'Minimo: ' + cast(@minPlayers as varchar(10));
    print 'Maximo: ' + cast(@maxPlayers as varchar(10));
    print 'Iniciando com: ' + cast(@proposed as varchar(10)) + ' efetivos.';

    while ((@proposed < @minPlayers) and (@continue = 1))
    begin
		if not exists (select 1 from #Balance where Balanced = 0)
		begin
			print 'Todos os clãs já balanceados. Sobrarão vagas...';
			set @continue = 0;
		end else
		begin
		   -- Seleciona o melhor para ser colocado com delay = 1
		   select top (1) @clanId = ClanId from #Balance where Balanced = 0 order by WN8t15 desc;
		   update #Balance set [Delay] = 1, Balanced = 1 where ClanId = @clanId;
	   
		   select @proposed = sum(EffectiveMembers) from #Balance;

		   print 'Iteração ' + cast(@iter as varchar(10)) + ': ' + cast(@proposed as varchar(10)) + ' efetivos. Clã Id ' + cast(@clanId as varchar(10)) + ' alterado para Delay 1.';	   

		   set @iter = @iter + 1;
	   end;
    end;
    
    if (@iter = 0) begin
	   print 'Nenhuma iteração. Há jogadores demais no sistema!';
    end;

	if (@debug = 1)
	begin
		select * from #Balance order by [Delay] desc, ClanTag;
	end;

    -- Quem Muda (resultado externo)
    select b.ClanId, b.ClanTag, c.[Delay] as OldDelay, b.[Delay] as NewDelay
	   from #Balance b
		  inner join Main.Clan c
			 on b.ClanId = c.ClanId
	   where b.[Delay] <> c.[Delay]
	   order by b.WN8t15 desc;

	if (@saveChanges = 1)
	begin

		-- Efetiva
		update Main.Clan
		   set [Delay] = b.[Delay]
		   from Main.Clan c
			  inner join #Balance b
				 on c.ClanId = b.ClanId
		   where c.[Delay] <> b.[Delay];

	end;

	if (@debug = 0)
	begin
		drop table #Balance;
		drop table #Innactive;
    end;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE proc [Main].[GetClanCalculateOrder]
    @ageHours    int = 24, -- Hours after a calculation is always due
	@minAgeHours int = 4   -- There's never to be a calculation before this number of hours
as
begin
    set nocount on;

    select 
			ClanTag, Max(PlayerMoment) as MaxPlayerMoment, ClanId,
			isnull(DATEDIFF(hour, Max(PlayerMoment), GETUTCDATE()), 6969) as MinPlayerMomentAgeHours
		into #ClanMaxPlayerMoment
		from Main.RecentClanCompositionStats
		where [Enabled] = 1
		group by ClanTag, ClanId
		order by MaxPlayerMoment;

	-- select * from #ClanMaxPlayerMoment order by MaxPlayerMomentAgeHours desc;

	select	
			c.ClanId, c.ClanTag, cu.CalculationMoment, cu.MembershipMoment, cmpm.MaxPlayerMoment,
			cu.CalculationAgeHours, cmpm.MinPlayerMomentAgeHours, cu.MembershipAgeHours
		from [Current].Clan cu
			inner join Main.Clan c
				on (c.ClanId = cu.ClanId)
			left outer join #ClanMaxPlayerMoment cmpm
				on (cmpm.ClanId = cu.ClanId)
			where	(c.[Enabled] = 1) and
					(
						(isnull(cu.CalculationMoment, '19740722') < cu.MembershipMoment) or
						(isnull(cu.CalculationMoment, '19740722') < isnull(cmpm.MaxPlayerMoment, '19740722')) or
						(isnull(cu.CalculationAgeHours, 6969) > @ageHours)
					)
					and isnull(cu.CalculationAgeHours, 6969) > @minAgeHours
		order by isnull(cu.CalculationMoment, '19740722') asc;

	drop table #ClanMaxPlayerMoment;

end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE proc [Main].[GetClanMembershipUpdateOrder] 
    @max int, 
    @ageHours int
as
begin

    set nocount on;
    
    select top (@max) ClanTag, MembershipMoment, MembershipAgeHours, ClanId
	   from
	   (
		  select  ClanTag, MembershipMoment,
				DATEDIFF(HOUR, MembershipMoment, GETUTCDATE()) as MembershipAgeHours,
				ClanId				
				from Main.RecentClanDate
				where ([Enabled] = 1)
				    and (DATEDIFF(HOUR, MembershipMoment, GETUTCDATE()) >= @ageHours)

		  union all	   

		  select ClanTag, null as MembershipMoment, null as MembershipAgeHours, ClanId from Main.Clan where [Enabled] = 1
		  except	   
		  select ClanTag, null as MembershipMoment, null as MembershipAgeHours, ClanId from Main.RecentClanDate where [Enabled] = 1
	   ) q
	   order by q.MembershipMoment;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE proc [Main].[GetPlayerUpdateOrder]
    @max int, 
    @ageHours int,
	@minAgeHours int = null
as
begin
	-- 2017-10-20: Dentro de uma "Idade Ajustada" a ordem fica aleatoria
	-- 2020-07-29: No more priority players via table

    set nocount on;

	if (@minAgeHours is null) 
	begin
		set @minAgeHours = 22
	end;

	-- Seta o Delay de quem está inativo para 4
	begin tran BalancePlayers;
		update Main.Player set [Delay] = 1.0;
		update Main.Player set [Delay] = 2.0 where PlayerId in (select PlayerId from Main.RecentPlayer where (MonthBattles > 0) and (MonthBattles < 21));
		update Main.Player set [Delay] = 5.0 where PlayerId in (select PlayerId from Main.RecentPlayer where MonthBattles <= 0);		
		-- ... mas jogadores recem adicionados tem 14 dias para provar se são ativos ou não
		update Main.Player set [Delay] = 1.0 where AgeDays <= 14;

		-- Seta para 1 o Delay de Players que doaram

		-- glrogers doou USD200 em 2019-09-06 (Perpétuo em 1.0!)
		update Main.Player set [Delay] = 1.0 where (PlayerId = 1081942686);

		-- Colonel2Can doou USD10 em 2019-09-06
		update Main.Player set [Delay] = 1.0 where (PlayerId = 6888845);

		
	commit tran BalancePlayers;

	declare @players table(
		[PlayerId] [bigint] NOT NULL,
		[GamerTag] [nvarchar](25) NOT NULL,
		[ClanTag] [varchar](5) NOT NULL,
		[AdjustedAgeHours] [float] NOT NULL,
		[PlayerMoment] [datetime] NULL,
		[ClanId] [bigint] NOT NULL,
		[AgeHours] [int] NULL,
		[Delay] [float] NOT NULL,
		[PlayerDelay] [float] NULL,
		[TotalDelay] [float] NULL,
		[Rand] uniqueidentifier,
		[PlayerPlatformId] int not null,
		[Order] [bigint] NULL
	);

	declare @queueLengh int;
	set @queueLengh = (select count(*) from @players);

	while ((@queueLengh < @max) and (@ageHours >= @minAgeHours))
	begin
		delete from @players;

		insert into @players
			select top (@max) *, ROW_NUMBER() over (order by AdjustedAgeHours desc, [Rand]) as [Order]
			from
			(
				select 
					PlayerId, GamerTag, ClanTag, ISNULL(AdjustedAgeHours, 6969) as AdjustedAgeHours, PlayerMoment, ClanId, AgeHours,
					q.[Delay], q.PlayerDelay, q.TotalDelay, NEWID() as [Rand], PlayerPlatformId
				from
				   (
				   select  DATEDIFF(hour, PlayerMoment, GETUTCDATE()) as PlayerAgeHours,
						 (1.0/[Delay]/PlayerDelay)*DATEDIFF(hour, PlayerMoment, GETUTCDATE()) as AdjustedAgeHours,
						 DATEDIFF(hour, PlayerMoment, GETUTCDATE()) as AgeHours,			
						 [Delay], PlayerDelay, TotalDelay, PlayerId, GamerTag, ClanTag, PlayerMoment, ClanId, [Enabled],
						 PlayerPlatformId
					  from Main.RecentClanCompositionStats
				   ) q 
				where ((AdjustedAgeHours >= @ageHours) or (AdjustedAgeHours is null)) and ([Enabled] = 1)
				   and (PlayerId not in (select PlayerId from Main.IgnoredPlayer))	
			) q
			order by AdjustedAgeHours desc, [Rand];

		set @queueLengh = (select count(*) from @players);	
		set @ageHours = @ageHours - 1;

		print 'queueLengh: ' + cast(@queueLengh as varchar(10));
		print '@ageHours:  ' + cast((@ageHours+1) as varchar(10));
	end;

	select * from @players order by [Order];

end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE proc [Main].[ReAddClan]
(
	@maxToAutoAdd int = null
)
as
begin
	set nocount on;

	set @maxToAutoAdd = ISNULL(@maxToAutoAdd, 5);

	select top (@maxToAutoAdd)
			ClanId, ClanTag
		into #ToReadd
		from Main.Clan 
		where ([Enabled] = 0) and (DisabledReason in (1, 2, 6))
		order by newid();

	update Main.Clan
		set [Enabled] = 1, DisabledReason = 0 
		from Main.Clan c
			inner join #ToReadd r
				on (r.ClanId = c.ClanId);

	drop table #ToReadd;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE proc [Main].[SetClanCalculation]
(
	@ClanId bigint,
	@Date date,
	@CalculationMoment datetime,
	@TotalMembers int,
	@ActiveMembers int,
	@TotalBattles int,
	@MonthBattles int,
	@TotalWinRate float,
	@MonthWinRate float,
	@TotalWN8 float,
	@WN8a float,
	@WN8t15 float,
	@WN8t7 float,
	@Top15AvgTier float = null,
	@ActiveAvgTier float = null,
	@TotalAvgTier float = null
)
as
begin
	set nocount on;

	set @Top15AvgTier = ISNULL(@Top15AvgTier, 10.0);
	set @ActiveAvgTier = ISNULL(@ActiveAvgTier, 10.0);
	set @TotalAvgTier = ISNULL(@TotalAvgTier, 10.0);

	begin tran SetClanCalculation;

	-- atualizo os calculos na tabela historica
	update Main.ClanDate set
			CalculationMoment = @CalculationMoment,
			TotalMembers = @TotalMembers,
			ActiveMembers = @ActiveMembers,
			TotalBattles = @TotalBattles,
			MonthBattles = @MonthBattles,
			WeekBattles = 0,
			TotalWinRate = @TotalWinRate,
			MonthWinrate = @MonthWinRate,
			WeekWinRate = 0,
			TotalWN8 = @TotalWN8,
			WN8a = @WN8a,
			WN8t15 = @WN8t15,
			WN8t7 = @WN8t7,
			Top15AvgTier = @Top15AvgTier,
			ActiveAvgTier = @ActiveAvgTier,
			TotalAvgTier = @TotalAvgTier
		where (ClanId = @ClanId) and ([Date] = @date);

	-- lido da tabela historica
	declare @MembershipMoment datetime;
	set @MembershipMoment = (select MembershipMoment from Main.ClanDate where (ClanId = @ClanId) and ([Date] = @Date));

	-- crio ou atualizo a tabela de registros correntes
	if exists (select 1 from [Current].Clan where (ClanId = @ClanId))
	begin
		-- atualizo com novos calculos
		
		update [Current].Clan set
				MembershipMoment = @MembershipMoment,
				CalculationMoment = @CalculationMoment,
				TotalMembers = @TotalMembers,
				ActiveMembers = @ActiveMembers,
				TotalBattles = @TotalBattles,
				MonthBattles = @MonthBattles,
				TotalWinRate = @TotalWinRate,
				MonthWinrate = @MonthWinRate,
				TotalWN8 = @TotalWN8,
				WN8a = @WN8a,
				WN8t15 = @WN8t15,
				WN8t7 = @WN8t7,
				Top15AvgTier = @Top15AvgTier,
				ActiveAvgTier = @ActiveAvgTier,
				TotalAvgTier = @TotalAvgTier
			where (ClanId = @ClanId);

	end else 
	begin
		-- insiro um novo registro
		insert into [Current].Clan (ClanId, MembershipMoment, CalculationMoment, 
				TotalMembers, ActiveMembers, TotalBattles, MonthBattles, TotalWinRate, MonthWinrate, 
				TotalWN8, WN8a, WN8t15, WN8t7, Top15AvgTier, ActiveAvgTier, TotalAvgTier)
			values (@ClanId, @MembershipMoment, @CalculationMoment,
				@TotalMembers, @ActiveMembers, @TotalBattles, @MonthBattles, @TotalWinRate, @MonthWinRate,
				@TotalWN8, @WN8a, @WN8t15, @WN8t7, @Top15AvgTier, @ActiveAvgTier, @TotalAvgTier);
	end;
	
	commit tran SetClanCalculation;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE proc [Main].[SetClanDate]
(
	@ClanId bigint,
	@Date date,
	@MembershipMoment datetime
)
as
begin
	set nocount on;

	begin tran SetClanDate;

	-- crio ou atualizo a tabela de registros historicos
	update Main.ClanDate set
				MembershipMoment = @MembershipMoment
			where (ClanId = @ClanId) and ([Date] = @Date);

	if @@ROWCOUNT <= 0
	begin
		-- insiro um novo registro
		insert into Main.ClanDate (ClanId, [Date], MembershipMoment)
			values (@ClanId, @Date, @MembershipMoment);
	end;
	
	-- crio ou atualizo a tabela de registros correntes
	update [Current].Clan set
				MembershipMoment = @MembershipMoment
			where (ClanId = @ClanId);
	if @@ROWCOUNT <= 0
	begin
		
		-- insiro um novo registro
		insert into [Current].Clan (ClanId, MembershipMoment)
			values (@ClanId, @MembershipMoment);
	end;
	
	commit tran SetClanDate;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE proc [Main].[SetPlayerDate]
(
	@PlayerId bigint,
	@Date date,
	@Moment datetime,
	@TotalBattles int,
	@MonthBattles int,
	@TotalWinRate float,
	@MonthWinRate float,
	@TotalWN8 float,
	@MonthWN8 float,
	@IsPatched bit,
	@Origin int,
	@TotalTier float = null,
	@MonthTier float = null,
	@Tier10TotalBattles int = null,
	@Tier10TotalWinRate float = null,
	@Tier10TotalWn8 float = null,
	@Tier10MonthBattles int = null,
	@Tier10MonthWinRate float = null,
	@Tier10MonthWn8 float = null
)
as
begin
	set nocount on;

	if (@TotalTier is null) set @TotalTier = 10;
	if (@MonthTier is null) set @MonthTier = 10;

	if (@Tier10TotalBattles is null) set @Tier10TotalBattles = 0;
	if (@Tier10TotalWinRate is null) set @Tier10TotalWinRate = 0.0;
	if (@Tier10TotalWn8 is null)     set @Tier10TotalWn8 = 0.0;

	if (@Tier10MonthBattles is null) set @Tier10MonthBattles = 0;
	if (@Tier10MonthWinRate is null) set @Tier10MonthWinRate = 0.0;
	if (@Tier10MonthWn8 is null)     set @Tier10MonthWn8 = 0.0;

	begin tran SetPlayerDate;

	-- Salva ou atualiza na tabela de historia
	if exists (select 1 from Main.PlayerDate where (PlayerId = @PlayerId) and ([Date] = @Date))
	begin
		update Main.PlayerDate set
				Moment = @Moment,
				TotalBattles = @TotalBattles, MonthBattles = @MonthBattles, WeekBattles = 0,
				TotalWinRate = @TotalWinRate, MonthWinRate = @MonthWinRate, WeekWinRate = 0,
				TotalWN8 =@TotalWN8, MonthWN8 = @MonthWN8, WeekWN8 = 0,
				IsPatched = @IsPatched, Origin = @Origin,
				TotalTier = @TotalTier, MonthTier = @MonthTier,
				Tier10TotalBattles = @Tier10TotalBattles, Tier10TotalWinRate = @Tier10TotalWinRate, Tier10TotalWN8 = @Tier10TotalWn8,
				Tier10MonthBattles = @Tier10MonthBattles, Tier10MonthWinRate = @Tier10MonthWinRate, Tier10MonthWN8 = @Tier10MonthWn8
			where (PlayerId = @PlayerId) and ([Date] = @Date);
	end else
	begin
		insert into Main.PlayerDate (PlayerId, [Date], Moment, 
				TotalBattles, MonthBattles, WeekBattles, 
				TotalWinRate, MonthWinRate, WeekWinRate,
				TotalWN8, MonthWN8, WeekWN8,
				IsPatched, Origin, TotalTier, MonthTier,
				Tier10TotalBattles, Tier10TotalWinRate, Tier10TotalWn8,
				Tier10MonthBattles, Tier10MonthWinRate, Tier10MonthWn8)
			values (@PlayerId, @Date, @Moment, 
				@TotalBattles, @MonthBattles, 0,
				@TotalWinRate, @MonthWinRate, 0,
				@TotalWN8, @MonthWN8, 0,
				@IsPatched, @Origin, @TotalTier, @MonthTier,
				@Tier10TotalBattles, @Tier10TotalWinRate, @Tier10TotalWn8,
				@Tier10MonthBattles, @Tier10MonthWinRate, @Tier10MonthWn8);
	end;

	-- Salva ou atualiza na tabela corrente
	if exists (select 1 from [Current].Player where (PlayerId = @PlayerId))
	begin
		update [Current].Player set
				Moment = @Moment,
				TotalBattles = @TotalBattles, MonthBattles = @MonthBattles, 
				TotalWinRate = @TotalWinRate, MonthWinRate = @MonthWinRate, 
				TotalWN8 =@TotalWN8, MonthWN8 = @MonthWN8, 
				IsPatched = @IsPatched, Origin = @Origin,
				TotalTier = @TotalTier, MonthTier = @MonthTier,
				Tier10TotalBattles = @Tier10TotalBattles, Tier10TotalWinRate = @Tier10TotalWinRate, Tier10TotalWN8 = @Tier10TotalWn8,
				Tier10MonthBattles = @Tier10MonthBattles, Tier10MonthWinRate = @Tier10MonthWinRate, Tier10MonthWN8 = @Tier10MonthWn8
			where (PlayerId = @PlayerId);
	end else
	begin
		insert into [Current].Player (PlayerId, Moment, 
				TotalBattles, MonthBattles, 
				TotalWinRate, MonthWinRate, 
				TotalWN8, MonthWN8, 
				IsPatched, Origin,
				TotalTier, MonthTier,
				Tier10TotalBattles, Tier10TotalWinRate, Tier10TotalWn8,
				Tier10MonthBattles, Tier10MonthWinRate, Tier10MonthWn8)
			values (@PlayerId, @Moment, 
				@TotalBattles, @MonthBattles,
				@TotalWinRate, @MonthWinRate,
				@TotalWN8, @MonthWN8,
				@IsPatched, @Origin,
				@TotalTier, @MonthTier,
				@Tier10TotalBattles, @Tier10TotalWinRate, @Tier10TotalWn8,
				@Tier10MonthBattles, @Tier10MonthWinRate, @Tier10MonthWn8);
	end;

	commit tran SetPlayerDate;

end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


CREATE proc [Performance].[CalculateMoEPercentile]
(
	@utcShift int = 0
)
as
begin
	-- 2020-07-26: No platform id

	set nocount on;
	declare @msg nvarchar(512);

	declare @method int;
	set @method = 4;

	declare @today datetime;
	set @today = DATEADD(hour, @utcShift, getutcdate());

	-- data maxima a executar
	declare @maxDate date;
	set @maxDate = DATEADD(day, -1, @today);
	set @msg = 'Max Date to Run: ' + convert(varchar(10), @maxDate, 126);
	raiserror(@msg, 0, 0) with nowait;
		
	-- data em que o processo está
	declare @lastDate date;
	set @lastDate = (select max([Date]) from [Performance].[MoE] where MethodId = @method);
	set @msg = 'Last Run Date: ' + convert(varchar(10), @lastDate, 126);
	raiserror(@msg, 0, 0) with nowait;

	-- Loop para calcular cada data que precisa de atualizacao
	declare @date date;
	set @date = DATEADD(day, 1, @lastDate);
	while (@date <= @maxDate)
	begin
		set @msg = 'Running for date: ' + convert(varchar(10), @date, 126);
		raiserror(@msg, 0, 1) with nowait;

		begin tran MoE4;

		delete from Performance.MoE where ([Date] = @date) and (MethodId = @method);

		insert into Performance.MoE ([Date], MethodId, TankId, MoE1Dmg, MoE2Dmg, MoE3Dmg, MaxAvgDmg, NumDates, Battles)
			select @date as [Date], @method as MethodId,
				q4.TankId,-- t.ShortName as Tank, t.Tier, tt.[Type], n.Nation, t.IsPremium,	
				(AvgPerc65Damage/0.65 + AvgPerc85Damage/0.85 + AvgPerc95Damage/0.95)/3.0*0.65 as MoE1Dmg,
				(AvgPerc65Damage/0.65 + AvgPerc85Damage/0.85 + AvgPerc95Damage/0.95)/3.0*0.85 as MoE2Dmg,
				(AvgPerc65Damage/0.65 + AvgPerc85Damage/0.85 + AvgPerc95Damage/0.95)/3.0*0.95 as MoE3Dmg,
				(AvgPerc65Damage/0.65 + AvgPerc85Damage/0.85 + AvgPerc95Damage/0.95)/3.0 as MaxAvgDmg,		
				NumDates, 	
				Battles
				--into Performance.MoE
				from
				(
				select 
					TankId, 
					avg(MaxDamage) as AvgMaxDamage, 
					avg(AvgDamage) as AvgAvgDamage, 
					avg(StdDevDamage) as AvgStdDevDamage, 
					avg(Perc50Damage) as AvgPerc50Damage,
					avg(Perc65Damage) as AvgPerc65Damage,
					avg(Perc85Damage) as AvgPerc85Damage,
					avg(Perc95Damage) as AvgPerc95Damage,
					count(*) as NumDates, 
					sum(Battles) as Battles
					from
					(
					select 
						distinct TankId, [Date], 
						max(Damage)		over (partition by TankId, [Date]) as MaxDamage, 
						avg(Damage)		over (partition by TankId, [Date]) as AvgDamage, 
						STDEVP(Damage)	over (partition by TankId, [Date]) as StdDevDamage, 
						sum(Battles)	over (partition by TankId, [Date]) as Battles,
						PERCENTILE_CONT(0.50) within group (order by Damage) over (partition by TankId, [Date]) as Perc50Damage,
						PERCENTILE_CONT(0.65) within group (order by Damage) over (partition by TankId, [Date]) as Perc65Damage,
						PERCENTILE_CONT(0.85) within group (order by Damage) over (partition by TankId, [Date]) as Perc85Damage,
						PERCENTILE_CONT(0.95) within group (order by Damage) over (partition by TankId, [Date]) as Perc95Damage
						from
						(
						select --top (100) --*
							PlayerId, TankId, [Date], 
							(TotalDamage - PreviousTotalDamage)/(Battles - PreviousBattles) as Damage,
							(Battles - PreviousBattles) as Battles
							from
							(
							select --top (100)
									PlayerId, TankId, [Date], 
									TotalDamage, Battles,
									lag([Date]) over (partition by PlayerId, TankId order by [Date]) as PreviousDate,
									lag(TotalDamage) over (partition by PlayerId, TankId order by [Date]) as PreviousTotalDamage,
									lag(Battles) over (partition by PlayerId, TankId order by [Date]) as PreviousBattles
								from Performance.PlayerDate p	
							) q
							where (PreviousBattles is not null)
								and (PreviousBattles < Battles)
								and (PreviousTotalDamage <= TotalDamage)
								and (DATEDIFF(day, PreviousDate, [Date]) = 1)
								and ([Date] <= @date)
								and ([Date] > DATEADD(day, -14, @date))
								--and (TankId = 6145) 
						) q2
						--group by TankId, [Date]
						--order by TankId, [Date];
					) q3
					group by TankId
				) q4 
					inner join Tanks.Tank t
						on (q4.TankId = t.TankId)			
				where t.Tier >= 5;		
		
		-- 2017-12-06, arredonda o High Mark para o multiplo de 50 abaixo, e corrige os demais para isso
		-- Sugestão do Pontiac Pat, via Discord
		update Performance.MoE
			set MaxAvgDmg = Floor(MaxAvgDmg/50.0)*50.0
			where [Date] = @date;

		update Performance.MoE
			set 
				MoE1Dmg = MaxAvgDmg*0.65,
				MoE2Dmg = MaxAvgDmg*0.85,
				MoE3Dmg = MaxAvgDmg*0.95
			where [Date] = @date;

		commit tran MoE4;

		set @date = DATEADD(day, 1, @date);	
	end;

end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


CREATE proc [Performance].[CalculateReferenceValues]
(
	@utcShift int = 0
)
as
begin
	declare @today datetime;
	set @today = DATEADD(hour, @utcShift, getutcdate());

	declare @maxDate date;
	set @maxDate = DATEADD(day, -1, @today);

	declare @maxDateOnDb date;
	set @maxDateOnDb = (select max([Date]) from Performance.ReferenceValues);

	declare @minDate date;
	set @minDate = DATEADD(day, +1, @maxDateOnDb);

	declare @currentDate date;
	set @currentDate = @minDate;

	while (@currentDate <= @maxDate)
	begin
		exec [Performance].CalculateReferenceValuesOnDate @currentDate;

		set @currentDate = DATEADD(day, +1, @currentDate);
	end;


end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE proc [Performance].[CalculateReferenceValuesOnDate]
(
	@maxDate date
)
as
begin
	set nocount on;

	-- Caso seja preciso desabilitar...
	--return;

	begin tran RefValues;

	declare @msg nvarchar(512);
	
	set @msg = 'Max Date to Run: ' + convert(varchar(10), @maxDate, 126);
	raiserror(@msg, 0, 0) with nowait;

	if exists (select 1 from Performance.ReferenceValues where [Date] = @maxDate)
	begin
		set @msg = 'Já executado para a data. Nada será feito.';
		raiserror(@msg, 0, 0) with nowait;	
		rollback tran RefValues;
		return;
	end;

	declare @monthDate date;
	set @monthDate = DATEADD(day, -28, @maxDate);
	set @msg = '28 days ago: ' + convert(varchar(10), @monthDate, 126);
	raiserror(@msg, 0, 0) with nowait;	
	
	-- Obter os Dados mais Recentes de Cada Jogador em cada tanque
	truncate table Performance.RecentByPlayer;

	insert into Performance.RecentByPlayer
	select q.*		
		from
		(
		SELECT 
				PlayerId, [Date], Moment, TankId, LastBattle, TreesCut, MaxFrags, MarkOfMastery, BattleLifeTimeSeconds,
				Spotted, DamageAssistedTrack, DamageAssistedRadio, DamageAssisted, CapturePoints, DroppedCapturePoints, Battles,
				Wins, Losses, Draws, WinRatio, Kills, SurvivedBattles, Deaths, DamageDealt, DamageReceived, TotalDamage,
				PiercingsReceived, Hits, NoDamageDirectHitsReceived, ExplosionHits, Piercings, Shots, 
				ExplosionHitsReceived, XP, DirectHitsReceived, 
				(ROW_NUMBER() over (partition by PlayerId, TankId order by [Date] desc)) as DateOrder
		  FROM [Performance].[PlayerDate]
		  where ([Date] <= @maxDate)
		) q
		where q.DateOrder = 1;

	set @msg = 'Performance.RecentByPlayer populated with ' + cast((select count(*) from Performance.RecentByPlayer) as varchar(15)) + ' records.';
	raiserror(@msg, 0, 0) with nowait;

	-- Calcular os totais de cada jogador
	truncate table Performance.RecentTotalsByPlayer;

	insert into Performance.RecentTotalsByPlayer
		select @maxDate as [Date], [PlayerId], 
			SUM([Battles]) as Battles, SUM(BattleLifeTimeSeconds) as BattleLifeTimeSeconds, sum(XP) as XP, sum(Wins) as Wins		
			from [Performance].[RecentByPlayer]
			group by [PlayerId];

	set @msg = 'Performance.RecentTotalsByPlayer populated with ' + cast((select count(*) from Performance.RecentTotalsByPlayer) as varchar(15)) + ' records.';
	raiserror(@msg, 0, 0) with nowait;

	-- Calcula os dados overall
	delete from Performance.ReferenceValues where ([Date] = @maxDate) and ([Period] = 'All');

	insert into Performance.ReferenceValues
		([Date], [Period], TankId,
		Spotted, DamageAssistedTrack, DamageAssistedRadio, CapturePoints, DroppedCapturePoints,
		TotalBattles, TotalPlayers, TotalWins, TotalLosses, Kills, TotalSurvivedBattles,
		DamageDealt, DamageReceived, BattleLifeTimeHours, TreesCut, MaxFrags, MarkOfMastery,
		Hits, NoDamageDirectHitsReceived, ExplosionHits, Piercings, Shots, ExplosionHitsReceived, 
		XP, DirectHitsReceived, PiercingsReceived)
	select 
			@maxDate as [Date], 
			'All' as [Period],
			p.TankId,			
			sum(Spotted)*1.0/sum(p.Battles) as Spotted,
			sum(DamageAssistedTrack)*1.0/sum(p.Battles) as DamageAssistedTrack,
			sum(DamageAssistedRadio)*1.0/sum(p.Battles) as DamageAssistedRadio,
			sum(CapturePoints)*1.0/sum(p.Battles) as CapturePoints,
			sum(DroppedCapturePoints)*1.0/sum(p.Battles) as DroppedCapturePoints,
			sum(p.Battles) as TotalBattles,
			count(distinct p.PlayerId) as TotalPlayers,
			sum(p.Wins) as TotalWins,
			sum(Losses) as TotalLosses,
			sum(Kills)*1.0/sum(p.Battles) as Kills,
			sum(SurvivedBattles) as TotalSurvivedBattles,
			sum(DamageDealt)*1.0/sum(p.Battles) as DamageDealt,
			sum(DamageReceived)*1.0/sum(p.Battles) as DamageReceived,
			sum(p.BattleLifeTimeSeconds/60.0/60.0) as BattleLifeTimeHours,
			sum(TreesCut)*1.0/sum(p.Battles) as TreesCut,
			sum(MaxFrags)*1.0/count(*) as MaxFrags,
			sum(MarkOfMastery)*1.0/count(*) as MarkOfMastery,
			sum(Hits)*1.0/sum(p.Battles) as Hits,
			sum(NoDamageDirectHitsReceived)*1.0/sum(p.Battles) as NoDamageDirectHitsReceived,
			sum(ExplosionHits)*1.0/sum(p.Battles) as ExplosionHits,
			sum(Piercings)*1.0/sum(p.Battles) as Piercings,
			sum(Shots)*1.0/sum(p.Battles) as Shots,
			sum(ExplosionHitsReceived)*1.0/sum(p.Battles) as ExplosionHitsReceived,
			sum(p.XP)*1.0/sum(p.Battles) as XP,
			sum(DirectHitsReceived)*1.0/sum(p.Battles) as DirectHitsReceived,
			sum(PiercingsReceived)*1.0/sum(p.Battles) as PiercingsReceived									
		from Performance.RecentByPlayer p
			inner join Performance.RecentTotalsByPlayer b
				on (p.PlayerId = b.PlayerId)
			inner join Tanks.Tank t on
				(t.TankId = p.TankId)
		where (b.Battles >= 1000) and (p.Battles >= (t.Tier*10)) and (p.XP > 0)
		group by p.TankId;

	set @msg = 'Performance.ReferenceValues (All) populated with ' + cast((select count(*) from Performance.ReferenceValues where ([Date] = @maxDate) and ([Period] = 'All')) as varchar(15)) + ' records.';
	raiserror(@msg, 0, 0) with nowait;

	-- popula os dados de um mes atras
	truncate table Performance.MonthAgoByPlayer;
	
	insert into Performance.MonthAgoByPlayer
	select q.*			
		from
		(
		SELECT 
				PlayerId, [Date], Moment, TankId, LastBattle, TreesCut, MaxFrags, MarkOfMastery, BattleLifeTimeSeconds,
				Spotted, DamageAssistedTrack, DamageAssistedRadio, DamageAssisted, CapturePoints, DroppedCapturePoints, Battles,
				Wins, Losses, Draws, WinRatio, Kills, SurvivedBattles, Deaths, DamageDealt, DamageReceived, TotalDamage,
				PiercingsReceived, Hits, NoDamageDirectHitsReceived, ExplosionHits, Piercings, Shots, 
				ExplosionHitsReceived, XP, DirectHitsReceived, 
				(ROW_NUMBER() over (partition by PlayerId, TankId order by [Date] desc)) as DateOrder
		  FROM [Performance].[PlayerDate]
		  where ([Date] <= @monthDate)
		) q
		where q.DateOrder = 1;

	set @msg = 'Performance.MonthAgoByPlayer populated with ' + cast((select count(*) from Performance.MonthAgoByPlayer) as varchar(15)) + ' records.';
	raiserror(@msg, 0, 0) with nowait;
	
	-- Calcula o Delta Mensal dos jogadores
	truncate table Performance.DeltaMonthByPlayer;
	
	insert into Performance.DeltaMonthByPlayer
	select --top (100)
			r.PlayerId, 
			r.[Date], ISNULL(p.[Date], r.[Date]) as PreviousDate,
			r.Moment, ISNULL(p.Moment, r.Moment) as PreviousMoment,
			r.TankId,
			r.LastBattle, isnull(p.LastBattle, r.LastBattle) as PreviousLastBattle,
			(r.TreesCut - isnull(p.TreesCut, 0)) as TreesCut,
			r.MaxFrags, isnull(p.MaxFrags, r.MaxFrags) as PreviousMaxFrags,
			r.MarkOfMastery, isnull(p.MarkOfMastery, r.MarkOfMastery) as PreviousMarkOfMastery,
			(r.BattleLifeTimeSeconds - isnull(p.BattleLifeTimeSeconds, 0)) as BattleLifeTimeSeconds,
			(r.Spotted - isnull(p.Spotted, 0)) as Spotted,
			(r.DamageAssistedTrack - isnull(p.DamageAssistedTrack, 0)) as DamageAssistedTrack,
			(r.DamageAssistedRadio - isnull(p.DamageAssistedRadio, 0)) as DamageAssistedRadio,
			(r.DamageAssisted - isnull(p.DamageAssisted, 0)) as DamageAssisted,
			(r.CapturePoints - isnull(p.CapturePoints, 0)) as CapturePoints,
			(r.DroppedCapturePoints - isnull(p.DroppedCapturePoints, 0)) as DroppedCapturePoints,
			(r.Battles - isnull(p.Battles, 0)) as Battles,
			r.Battles as RecentBattles,
			(r.Wins - isnull(p.Wins, 0)) as Wins,
			(r.Losses - isnull(p.Losses, 0)) as Losses,
			(r.Draws - isnull(p.Draws, 0)) as Draws,
			(r.Kills - isnull(p.Kills, 0)) as Kills,
			(r.SurvivedBattles - isnull(p.SurvivedBattles, 0)) as SurvivedBattles,
			(r.Deaths - isnull(p.Deaths, 0)) as Deaths,
			(r.DamageDealt - isnull(p.DamageDealt, 0)) as DamageDealt,
			(r.DamageReceived - isnull(p.DamageReceived, 0)) as DamageReceived,
			(r.TotalDamage - isnull(p.TotalDamage, 0)) as TotalDamage,
			(r.PiercingsReceived - isnull(p.PiercingsReceived, 0)) as PiercingsReceived,
			(r.Hits - isnull(p.Hits, 0)) as Hits,
			(r.NoDamageDirectHitsReceived - isnull(p.NoDamageDirectHitsReceived, 0)) as NoDamageDirectHitsReceived,
			(r.ExplosionHits - isnull(p.ExplosionHits, 0)) as ExplosionHits,
			(r.Piercings - isnull(p.Piercings, 0)) as Piercings,
			(r.Shots - isnull(p.Shots, 0)) as Shots,
			(r.ExplosionHitsReceived - isnull(p.ExplosionHitsReceived, 0)) as ExplosionHitsReceived,
			(r.XP - isnull(p.XP, 0)) as XP,
			(r.DirectHitsReceived - isnull(p.DirectHitsReceived, 0)) as DirectHitsReceived					
		from Performance.RecentByPlayer r
			left outer join Performance.MonthAgoByPlayer p
				on (r.PlayerId = p.PlayerId) and (r.TankId = p.TankId)
		where 
			(r.Battles > 0) and (r.XP > 0) and (isnull(p.XP, 0) > 0) and
			(r.Battles - isnull(p.Battles, 0)) > 0;

	set @msg = 'Performance.DeltaMonthByPlayer populated with ' + cast((select count(*) from Performance.DeltaMonthByPlayer) as varchar(15)) + ' records.';
	raiserror(@msg, 0, 0) with nowait;			

	-- calcula os dados mensais
	delete from Performance.ReferenceValues where ([Date] = @maxDate) and ([Period] = 'Month');

	-- Popula a performance mensal
	insert into Performance.ReferenceValues
			([Date], [Period], TankId,
			Spotted, DamageAssistedTrack, DamageAssistedRadio, CapturePoints, DroppedCapturePoints,
			TotalBattles, TotalPlayers, TotalWins, TotalLosses, Kills, TotalSurvivedBattles,
			DamageDealt, DamageReceived, BattleLifeTimeHours, TreesCut, MaxFrags, MarkOfMastery,
			Hits, NoDamageDirectHitsReceived, ExplosionHits, Piercings, Shots, ExplosionHitsReceived, 
			XP, DirectHitsReceived, PiercingsReceived)
	select 
			@maxDate as [Date], 
			'Month' as [Period],
			p.TankId,			
			sum(Spotted)*1.0/sum(p.Battles) as Spotted,
			sum(DamageAssistedTrack)*1.0/sum(p.Battles) as DamageAssistedTrack,
			sum(DamageAssistedRadio)*1.0/sum(p.Battles) as DamageAssistedRadio,
			sum(CapturePoints)*1.0/sum(p.Battles) as CapturePoints,
			sum(DroppedCapturePoints)*1.0/sum(p.Battles) as DroppedCapturePoints,
			sum(p.Battles) as TotalBattles,
			count(distinct p.PlayerId) as TotalPlayers,
			sum(p.Wins) as TotalWins,
			sum(Losses) as TotalLosses,
			sum(Kills)*1.0/sum(p.Battles) as Kills,
			sum(SurvivedBattles) as TotalSurvivedBattles,
			sum(DamageDealt)*1.0/sum(p.Battles) as DamageDealt,
			sum(DamageReceived)*1.0/sum(p.Battles) as DamageReceived,
			sum(p.BattleLifeTimeSeconds/60.0/60.0) as BattleLifeTimeHours,
			sum(TreesCut)*1.0/sum(p.Battles) as TreesCut,
			sum(MaxFrags)*1.0/count(*) as MaxFrags,
			sum(MarkOfMastery)*1.0/count(*) as MarkOfMastery,
			sum(Hits)*1.0/sum(p.Battles) as Hits,
			sum(NoDamageDirectHitsReceived)*1.0/sum(p.Battles) as NoDamageDirectHitsReceived,
			sum(ExplosionHits)*1.0/sum(p.Battles) as ExplosionHits,
			sum(Piercings)*1.0/sum(p.Battles) as Piercings,
			sum(Shots)*1.0/sum(p.Battles) as Shots,
			sum(ExplosionHitsReceived)*1.0/sum(p.Battles) as ExplosionHitsReceived,
			sum(p.XP)*1.0/sum(p.Battles) as XP,
			sum(DirectHitsReceived)*1.0/sum(p.Battles) as DirectHitsReceived,
			sum(PiercingsReceived)*1.0/sum(p.Battles) as PiercingsReceived						
		from Performance.DeltaMonthByPlayer p
			inner join Performance.RecentTotalsByPlayer b
				on (p.PlayerId = b.PlayerId)
			inner join Tanks.Tank t
				on (t.TankId = p.TankId)
		where (b.Battles >= 1000) and (p.RecentBattles >= (t.Tier*10)) and (p.Battles > 0) and (p.XP > 0)
		group by p.TankId;

		set @msg = 'Performance.ReferenceValues (Month) populated with ' + cast((select count(*) from Performance.ReferenceValues where ([Date] = @maxDate) and ([Period] = 'Month')) as varchar(15)) + ' records.';
		raiserror(@msg, 0, 0) with nowait;

	commit tran RefValues;
		
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE proc [Performance].[GetHistogramAll]
(
	@tankId int,
	@metric varchar(50),
	@levels smallint = 50
)
as
begin

	declare @values table
	(
		[value] numeric(38, 18)
	);

	if (@metric = 'Kills')
		insert into @values ([value])
			select r.KillsPerBattle
				from [Performance].RecentByPlayer r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.Battles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'DamageDealt')
		insert into @values ([value])
			select r.DamageDealtPerBattle
				from [Performance].RecentByPlayer r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.Battles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'Spotted')
		insert into @values ([value])
			select r.SpottedPerBattle
				from [Performance].RecentByPlayer r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.Battles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'DroppedCapturePoints')
		insert into @values ([value])
			select r.DroppedCapturePointsPerBattle
				from [Performance].RecentByPlayer r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.Battles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'DamageAssistedTrack')
		insert into @values ([value])
			select r.DamageAssistedTrackPerBattle
				from [Performance].RecentByPlayer r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.Battles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'DamageAssistedRadio')
		insert into @values ([value])
			select r.DamageAssistedRadioPerBattle
				from [Performance].RecentByPlayer r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.Battles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'DamageAssisted')
		insert into @values ([value])
			select r.DamageAssistedPerBattle
				from [Performance].RecentByPlayer r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.Battles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'TotalDamage')
		insert into @values ([value])
			select r.TotalDamagePerBattle
				from [Performance].RecentByPlayer r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.Battles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'WinRatio')
		insert into @values ([value])
			select r.WinRatio
				from [Performance].RecentByPlayer r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.Battles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else begin
		return;
	end;

	DECLARE @interval numeric(38, 18), --- How "wide" each range is
       		@min      numeric(38, 18), --- The lowest value in the table
       		@max      numeric(38, 18); --- The highest value in the table

	--- Check the source data for MIN(), MAX() and COUNT(). This
	--- is a highly efficient stream aggregate.
	SELECT @max=MAX([value]),
      	   @min=MIN([value])
	FROM @values;

	--- Finally, calculate @interval:
	SET @interval=(@max-@min)/@levels;

	--print @min;
	--print @max;
	--print @interval;

	SELECT @min+(value-value%@interval)           AS FromValue,
       	   @min+(value-value%@interval)+@interval AS ToValue,
       	   COUNT(*) AS [Count]
	FROM (
		  --- If value is @max, set value to @max-0.5*@interval
		  --- Then subtract @min.
		  SELECT ISNULL(NULLIF(value, @max), @max-0.5*@interval)-@min AS value
		  FROM @values
		  ) AS sub
	GROUP BY value-value%@interval
	ORDER BY value-value%@interval;

end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE proc [Performance].[GetHistogramMonth]
(
	@tankId int,
	@metric varchar(50),
	@levels smallint = 50
)
as
begin

	declare @values table
	(
		[value] numeric(38, 18)
	);

	if (@metric = 'Kills')
		insert into @values ([value])
			select r.KillsPerBattle
				from [Performance].[DeltaMonthByPlayer] r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.RecentBattles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'DamageDealt')
		insert into @values ([value])
			select r.DamageDealtPerBattle
				from [Performance].[DeltaMonthByPlayer] r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.RecentBattles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'Spotted')
		insert into @values ([value])
			select r.SpottedPerBattle
				from [Performance].[DeltaMonthByPlayer] r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.RecentBattles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'DroppedCapturePoints')
		insert into @values ([value])
			select r.DroppedCapturePointsPerBattle
				from [Performance].[DeltaMonthByPlayer] r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.RecentBattles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'DamageAssistedTrack')
		insert into @values ([value])
			select r.DamageAssistedTrackPerBattle
				from [Performance].[DeltaMonthByPlayer] r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.RecentBattles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'DamageAssistedRadio')
		insert into @values ([value])
			select r.DamageAssistedRadioPerBattle
				from [Performance].[DeltaMonthByPlayer] r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.RecentBattles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'DamageAssisted')
		insert into @values ([value])
			select r.DamageAssistedPerBattle
				from [Performance].[DeltaMonthByPlayer] r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.RecentBattles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'TotalDamage')
		insert into @values ([value])
			select r.TotalDamagePerBattle
				from [Performance].[DeltaMonthByPlayer] r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.RecentBattles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else if (@metric = 'WinRatio')
		insert into @values ([value])
			select r.WinRatio
				from [Performance].[DeltaMonthByPlayer] r
					inner join [Performance].[RecentTotalsByPlayer] t
						on (r.PlayerId = t.PlayerId)
					inner join Tanks.Tank tt
						on (tt.TankId = r.TankId)
				where (r.RecentBattles >= (tt.Tier*10)) and (t.Battles >= 1000) and (r.TankId = @tankId);	
	else begin
		return;
	end;

	DECLARE @interval numeric(38, 18), --- How "wide" each range is
       		@min      numeric(38, 18), --- The lowest value in the table
       		@max      numeric(38, 18); --- The highest value in the table

	--- Check the source data for MIN(), MAX() and COUNT(). This
	--- is a highly efficient stream aggregate.
	SELECT @max=MAX([value]),
      	   @min=MIN([value])
	FROM @values;

	--- Finally, calculate @interval:
	SET @interval=(@max-@min)/@levels;

	--print @min;
	--print @max;
	--print @interval;

	SELECT @min+(value-value%@interval)           AS FromValue,
       	   @min+(value-value%@interval)+@interval AS ToValue,
       	   COUNT(*) AS [Count]
	FROM (
		  --- If value is @max, set value to @max-0.5*@interval
		  --- Then subtract @min.
		  SELECT ISNULL(NULLIF(value, @max), @max-0.5*@interval)-@min AS value
		  FROM @values
		  ) AS sub
	GROUP BY value-value%@interval
	ORDER BY value-value%@interval;

end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


CREATE proc [Performance].[GetMoE] 
(
	@methodId int,
	@date date = null,
	@tankId bigint = null
) 
as
begin
	-- 2020-07-26: No platform anymore

	set nocount on;

	declare @maxDate date;

	if (@date is null)
	begin
		set @maxDate = (select max([Date]) from Performance.MoE where (MethodId = @methodId));
	end else begin
		set @maxDate = @date;
	end;

	select 
		m.[Date],
		t.ShortName, t.Tier, t.TypeId, tt.[Type], t.NationId, n.Nation, t.IsPremium, t.Tag,
		--m.MoE1Dmg, m.MoE2Dmg, m.MoE3Dmg, 
		m.MaxAvgDmg,
		m.NumDates, m.Battles, m.TankId, t.[Name]
	from Performance.Moe m
		inner join Tanks.Tank t
				on (m.TankId = t.TankId)
			inner join Tanks.Nation n
				on (n.NationId = t.NationId)
			inner join Tanks.[Type] tt
				on tt.TypeId = t.TypeId
	where ([Date] = @maxDate) and (MethodId = @methodId) and (m.TankId = ISNULL(@tankId, m.TankId))
	order by t.Tier desc, tt.TypeId, n.Nation, t.ShortName;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


CREATE proc [Performance].[GetTanksReferences]
(
	@period varchar(5),
	@date date = null,
	@tankId bigint = null
)
as
begin
	-- 2020-07-27: No platfor, as all is Console
	
	set nocount on;

	declare @maxDate date;

	if (@date is null)
	begin
		set @maxDate = (select max([Date]) from Performance.ReferenceValues where [Period] = @period);
	end else begin
		set @maxDate = @date;
	end;

	
	select 
		m.[Date], m.[Period], m.TankId,
		t.ShortName, t.Tier, t.TypeId, tt.[Type], t.NationId, n.Nation, t.IsPremium, t.Tag,
		m.Spotted, m.DamageAssistedTrack, m.DamageAssistedRadio, m.CapturePoints, m.DroppedCapturePoints,
		m.TotalBattles, m.TotalPlayers, m.TotalWins, m. TotalLosses, m.Kills, m.TotalSurvivedBattles,
		m.DamageDealt, m.DamageReceived, m.BattleLifeTimeHours, m.TreesCut, m.MaxFrags, m.MarkOfMastery,
		m.Hits, m.NoDamageDirectHitsReceived, m.ExplosionHits, m.Piercings, m.Shots, m.ExplosionHitsReceived,
		m.XP, m.DirectHitsReceived, m.PiercingsReceived,
		t.[Name]
	from Performance.ReferenceValues m
		inner join Tanks.Tank t
				on (m.TankId = t.TankId)
			inner join Tanks.Nation n
				on (n.NationId = t.NationId)
			inner join Tanks.[Type] tt
				on tt.TypeId = t.TypeId
	where ([Date] = @maxDate) and ([Period] = @period) and (t.TankId = ISNULL(@tankId, t.TankId))
	order by t.Tier desc, tt.TypeId, n.Nation, t.ShortName;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


CREATE proc [Performance].[GetTopPlayersAll]
(
	@tankId int,
	@top int,
	@flagCode char(2) = null,
	@skip int = 0,
	@months int = 3,
	@tierFactor int = 10,
	@date date = null
)
as
begin
	if (@date is null) begin
		set @date = (select max([Date]) from [Performance].[RecentByPlayer]);
	end;

	select 
			r.PlayerId, p.GamerTag, 
			rcc.ClanId, rcc.ClanTag, rcc.FlagCode, 
			r.LastBattle, r.BattleLifeTimeSeconds, r.Battles, 
			r.TotalDamagePerBattle, r.[DamageAssistedPerBattle], r.[KillsPerBattle], r.[MaxFrags], 
			r.[SpottedPerBattle], @date as [Date],
			tt.ShortName, tt.[Name], tt.IsPremium, tt.Tag, tt.Tier, tt.TypeId, tt.NationId, p.PlataformId as PlayerPlatform
		from [Performance].[RecentByPlayer] r
			inner join [Performance].[RecentTotalsByPlayer] t
				on (r.PlayerId = t.PlayerId)
			inner join Tanks.Tank tt
				on (tt.TankId = r.TankId)
			inner join Main.Player p
				on (p.PlayerId = r.PlayerId)
			inner join [Main].[RecentClanComposition] rcc
				on (rcc.PlayerId = r.PlayerId)
		where (r.Battles >= (tt.Tier*@tierFactor)) and (t.Battles >= 1000) and (r.TankId = @tankId)
			and (r.LastBattle >= DATEADD(day, -28*@months, @date))
			and (rcc.[Enabled] = 1)
			and (ISNULL(rcc.FlagCode, 'xxx') = ISNULL(@flagCode, ISNULL(rcc.FlagCode, 'xxx')))
		order by r.TotalDamagePerBattle desc
		offset @skip rows
		fetch next @top rows only;
end;	
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE proc [Performance].[HasAnyData]
(
	@playerId bigint
)
as
begin
	set nocount on;
	
	if exists (select 1 from Performance.PlayerDate where (PlayerId = @playerId))
		select 1
	else 
		select 0;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE proc [Performance].[SetPlayerDate]
(
	@PlayerId bigint,
	@Date date,
	@Moment datetime,
	@TankId bigint,
	@LastBattle datetime,
	@TreesCut bigint,
	@MaxFrags bigint,
	@MarkOfMastery bigint,
	@BattleLifeTimeSeconds bigint,
	@Spotted bigint,
	@DamageAssistedTrack bigint,
	@DamageAssistedRadio bigint,
	@CapturePoints bigint,
	@DroppedCapturePoints bigint,
	@Battles bigint,
	@Wins bigint,
	@Losses bigint,
	@Kills bigint,
	@SurvivedBattles bigint,
	@DamageDealt bigint,
	@DamageReceived bigint,
	@PiercingsReceived bigint = 0,
	@Hits bigint = 0,
	@NoDamageDirectHitsReceived bigint = 0,
	@ExplosionHits bigint = 0,
	@Piercings bigint = 0,
	@Shots bigint = 0,
	@ExplosionHitsReceived bigint = 0,
	@XP bigint = 0,
	@DirectHitsReceived bigint = 0,
	@IsAnchor bit = 0
)
as
begin
	set nocount on;

	begin tran PerfSetPlayerDate;

	if exists (select 1 from Performance.PlayerDate where (PlayerId = @PlayerId) and ([Date] = @Date) and (TankId = @TankId))
	begin
		
		-- Só havera atualização se realmente houver nova informação
		declare @existingLastBattle datetime;
		declare @existingBattles bigint;
		declare @existingXP bigint;

		select @existingLastBattle = LastBattle, @existingBattles = Battles, @existingXP = XP
			from Performance.PlayerDate
			where (PlayerId = @PlayerId) and (TankId = @TankId) and ([Date] = @Date);

		if ((@existingLastBattle != @LastBattle)
			or
			(@existingBattles != @Battles)
			or
			(@existingXP != @XP))
		begin

			update Performance.PlayerDate
				set 
					Moment = @Moment,
					LastBattle = @LastBattle,
					TreesCut = @TreesCut,
					MaxFrags = @MaxFrags,
					MarkOfMastery = @MarkOfMastery,
					BattleLifeTimeSeconds = @BattleLifeTimeSeconds,
					Spotted = @Spotted,
					DamageAssistedTrack = @DamageAssistedTrack,
					DamageAssistedRadio = @DamageAssistedRadio,
					CapturePoints = @CapturePoints,
					DroppedCapturePoints = @DroppedCapturePoints,
					Battles = @Battles,
					Wins = @Wins,
					Losses = @Losses,
					Kills = @Kills,
					SurvivedBattles = @SurvivedBattles,
					DamageDealt = @DamageDealt,
					DamageReceived = @DamageReceived,
					PiercingsReceived = @PiercingsReceived,
					Hits = @Hits,
					NoDamageDirectHitsReceived = @NoDamageDirectHitsReceived,
					ExplosionHits = @ExplosionHits,
					Piercings = @Piercings,
					Shots = @Shots,
					ExplosionHitsReceived = @ExplosionHitsReceived,
					XP = @XP,
					DirectHitsReceived = @DirectHitsReceived,
					IsAnchor = @IsAnchor
				where (PlayerId = @PlayerId) and ([Date] = @Date) and (TankId = @TankId);		
		end;
	end else
	begin
		
		-- Há nova informação que merece ser salva
		INSERT INTO Performance.PlayerDate
			(PlayerId, [Date], Moment, TankId, LastBattle, TreesCut, MaxFrags, 
			MarkOfMastery, BattleLifeTimeSeconds, Spotted, DamageAssistedTrack, DamageAssistedRadio, 
			CapturePoints, DroppedCapturePoints, Battles, Wins, Losses, Kills, SurvivedBattles, DamageDealt, DamageReceived,
			PiercingsReceived, Hits, NoDamageDirectHitsReceived, ExplosionHits, Piercings, 
			Shots, ExplosionHitsReceived, XP, DirectHitsReceived, IsAnchor)
		VALUES
			(@PlayerId, @Date, @Moment, @TankId, @LastBattle, @TreesCut, @MaxFrags,
			@MarkOfMastery, @BattleLifeTimeSeconds, @Spotted, @DamageAssistedTrack, @DamageAssistedRadio, 
			@CapturePoints, @DroppedCapturePoints, @Battles, @Wins, @Losses, @Kills, @SurvivedBattles, 
			@DamageDealt, @DamageReceived, @PiercingsReceived, @Hits, @NoDamageDirectHitsReceived, @ExplosionHits, 
			@Piercings, @Shots, @ExplosionHitsReceived, @XP, @DirectHitsReceived, @IsAnchor)
		
	end;

	commit tran PerfSetPlayerDate;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE proc [Store].[SetClan]
(
	@clanId bigint,
	@data varbinary(max)
)
as
begin
	set nocount on;

	begin tran Store_SetClan;

	if exists (select 1 from Store.Clan where ClanId = @clanId)
	begin
		update Store.Clan
			set 
				Moment = GETUTCDATE(),
				BinData = @data
			where ClanId = @clanId;
	end else
	begin
		insert into Store.Clan(ClanId, Moment, BinData) values (@clanId, GETUTCDATE(), @data);
	end;

	commit tran Store_SetClan;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create proc [Store].[SetGeneral]
(
	@key varchar(15),
	@data nvarchar(max)
)
as
begin
	set nocount on;

	begin tran Store_SetGeneral;

	if exists (select 1 from Store.General where [Key] = @key)
	begin
		update Store.General
			set 
				Moment = GETUTCDATE(),
				[Data] = @data
			where [Key] = @key;
	end else
	begin
		insert into Store.General ([Key], [Moment], [Data]) values (@key, GETUTCDATE(), @data)
	end;

	commit tran Store_SetGeneral;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE proc [Store].[SetPlayer]
(
	@playerId bigint,
	@data varbinary(max)
)
as
begin
	set nocount on;

	begin tran Store_SetPlayer;

	if exists (select 1 from Store.Player where PlayerId = @playerId)
	begin
		update Store.Player
			set 
				Moment = GETUTCDATE(),
				BinData = @data
			where PlayerId = @playerId;
	end else
	begin
		insert into Store.Player(PlayerId, Moment, BinData) values (@playerId, GETUTCDATE(), @data);
	end;

	commit tran Store_SetPlayer;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


-- Cria a procedure de desfragmentacao
create proc [Support].[DefragIndexes]
as
begin

	-- seleciona todos os indices que estejam fragmentados
	declare @method sysname
	set @method = 'SAMPLED'

	declare @noneLimit float
	set @noneLimit = 5.0

	declare @rebuildLimit float
	set @rebuildLimit = 30.0

	declare @minRecords int
	set @minRecords = 500

	select --top 10
		quotename(s.name) as [Schema], quotename(o.name) as [Table], quotename(i.name) as [Index],
			i.type_desc, c.name as [Column],
			ips.alloc_unit_type_desc, ips.avg_fragmentation_in_percent, ips.record_count, ips.avg_record_size_in_bytes,
			ips.page_count, ips.avg_page_space_used_in_percent, df.[definition],
			case
				when ips.avg_fragmentation_in_percent  <  @noneLimit then 'none'
				when  ips.avg_fragmentation_in_percent >= @rebuildLimit then 'rebuild'
				else 'reorganize'
			end as [Action]
		into #work_to_do
		from sys.objects o
			inner join sys.schemas s
				on o.schema_id = s.schema_id
			inner join sys.indexes i
				on i.object_id = o.object_id
			inner join sys.index_columns ic
				on ic.index_id = i.index_id and ic.object_id = o.object_id
			inner join sys.columns c
				on c.object_id = o.object_id and c.column_id = ic.column_id
			inner join sys.dm_db_index_physical_stats(db_id(), null, null, null, @method) ips
				on ips.object_id = o.object_id and ips.index_id = i.index_id
			left outer join sys.default_constraints df
				on df.object_id = o.object_id and df.parent_column_id = c.column_id
		where (o.type = 'U') -- and (c.system_type_id = 36)
			and (ips.record_count >= @minRecords) and (ips.avg_fragmentation_in_percent >= @noneLimit)
		order by
			--s.name, o.name, i.name, ic.key_ordinal
			ips.record_count desc, ips.avg_fragmentation_in_percent desc

	-- recordset com os comandos que serão emitidos
	select distinct 'alter index ' + [Index] + ' on ' + [Schema]+ '.' + [Table] + ' ' + [Action],
		avg_fragmentation_in_percent, record_count
			from #work_to_do
			where [Action] not in ('none')
			order by avg_fragmentation_in_percent desc, record_count desc

	declare @command nvarchar(4000)

	declare jobs cursor for
		select distinct 'alter index ' + [Index] + ' on ' + [Schema]+ '.' + [Table] + ' ' + [Action]
			from #work_to_do
			where [Action] not in ('none')

	open jobs

	while (1=1) begin
		fetch next from jobs into @command
		if @@fetch_status < 0 break

		exec(@command)
		print 'Executado: ' + @command
	end

	close jobs
	deallocate jobs

	drop table #work_to_do

	-- recordset com os resultante
	select --top 10
		quotename(s.name) as [Schema], quotename(o.name) as [Table], quotename(i.name) as [Index],
			i.type_desc, c.name as [Column],
			ips.alloc_unit_type_desc, ips.avg_fragmentation_in_percent, ips.record_count, ips.avg_record_size_in_bytes,
			ips.page_count, ips.avg_page_space_used_in_percent, df.[definition],
			case
				when ips.avg_fragmentation_in_percent  <  @noneLimit then 'none'
				when  ips.avg_fragmentation_in_percent >= @rebuildLimit then 'rebuild'
				else 'reorganize'
			end as [Action]
		from sys.objects o
			inner join sys.schemas s
				on o.schema_id = s.schema_id
			inner join sys.indexes i
				on i.object_id = o.object_id
			inner join sys.index_columns ic
				on ic.index_id = i.index_id and ic.object_id = o.object_id
			inner join sys.columns c
				on c.object_id = o.object_id and c.column_id = ic.column_id
			inner join sys.dm_db_index_physical_stats(db_id(), null, null, null, @method) ips
				on ips.object_id = o.object_id and ips.index_id = i.index_id
			left outer join sys.default_constraints df
				on df.object_id = o.object_id and df.parent_column_id = c.column_id
		where (o.type = 'U') --and (c.system_type_id = 36)
			and (ips.record_count >= @minRecords) and (ips.avg_fragmentation_in_percent >= @noneLimit)
		order by
			--s.name, o.name, i.name, ic.key_ordinal
			ips.record_count desc, ips.avg_fragmentation_in_percent desc


	-- atualiza estatisticas
	--exec sp_updatestats No sql 2005 somente o proprio DBO e membros de sysadmin podem chamar essa procedure, paciencia...

end


GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Relatório de tamanho da base de dados
-- 2014-10-16: Modificado para suportar BDs com mais de 2 bilhões de registros numa tabela
CREATE proc [Support].[GetAllTablesSpace]
as
begin
	set nocount on;

	DECLARE @table_name VARCHAR(500);
	DECLARE @schema_name VARCHAR(500);
	declare @scopeIdentity int;

	declare @temp_Table TABLE (
		tablename sysname
		,SchemaName VARCHAR(500)
		,row_count BIGINT
		,reserved VARCHAR(50) collate database_default
		,data VARCHAR(50) collate database_default
		,index_size VARCHAR(50) collate database_default
		,unused VARCHAR(50) collate database_default
		,TotalSizeKB BIGINT null
		,DataSizeKB BIGINT null
		,IndexSizeKB BIGINT null
		,UnusedSizeKB BIGINT null
		,RowNr int IDENTITY(1,1) Not NULL
	);

	DECLARE c1 CURSOR FOR
	    SELECT Table_Schema, Table_Name
	        FROM information_schema.tables t1
	        WHERE TABLE_TYPE = 'BASE TABLE';

	OPEN c1;
	FETCH NEXT FROM c1 INTO @schema_name, @table_name;
	WHILE @@FETCH_STATUS = 0
	BEGIN
			SET @table_name = @schema_name + '.' + @table_name;
			SET @table_name = REPLACE(@table_name, '[','');
			SET @table_name = REPLACE(@table_name, ']','');

			-- make sure the object exists before calling sp_spacedused
			IF EXISTS(SELECT id FROM sysobjects WHERE id = OBJECT_ID(@table_name))
			BEGIN
				   INSERT INTO @temp_Table (tablename, row_count, reserved, data, index_size, unused) EXEC sp_spaceused @table_name, false;

				   set @scopeIdentity = (select scope_identity());

					update @temp_Table set
						SchemaName=@schema_name,
						TotalSizeKB = cast(left(reserved, len(reserved)-3) as BIGINT),
						DataSizeKB = cast(left(data, len(data)-3) as BIGINT),
						IndexSizeKB = cast(left(index_size, len(index_size)-3) as BIGINT),
						UnusedSizeKB = cast(left(unused, len(unused)-3) as BIGINT)
					where RowNr = @scopeIdentity;
			END

			FETCH NEXT FROM c1 INTO @schema_name, @table_name;
	END
	CLOSE c1;
	DEALLOCATE c1;

	SELECT  SchemaName, TableName, t1.row_count as NumRows, t1.TotalSizeKB, t1.DataSizeKB, t1.IndexSizeKB, t1.UnusedSizeKB
		FROM @temp_Table t1
		ORDER BY TotalSizeKB desc, SchemaName, TableName;
end;

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Procedure para tirar completamente do BD jogadores que não jogam a mais de 1 ano e 1 mês	(meses lunares, 28 dias)
CREATE proc [Support].[PurgeOldPlayers]
as
begin
	set nocount on;

	select [PlayerId], max([LastBattle]) as LastBattle
		into #LastBattle
		from [Performance].[PlayerDate]	
		group by [PlayerId]
		order by max([LastBattle]) desc;

	select *, DATEDIFF(day, LastBattle, GETUTCDATE()) as Age
		into #ToPurge
		from #LastBattle
		where DATEDIFF(day, LastBattle, GETUTCDATE()) > (28 * 12)
		order by Age desc;

	-- não tem cascade...
	delete Main.ClanDatePlayer
		from Main.ClanDatePlayer t
		inner join #ToPurge p
					on p.PlayerId = t.PlayerId;


	-- não tem cascade...
	delete [Current].ClanPlayer
		from [Current].ClanPlayer t
		inner join #ToPurge p
					on p.PlayerId = t.PlayerId;

	delete Main.Player
		from Main.Player t
		inner join #ToPurge p
					on p.PlayerId = t.PlayerId;

	select * from #ToPurge;

	drop table #ToPurge;
	drop table #LastBattle;

end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE proc [Support].[PurgePlayer]
(
	@id bigint
)
as
begin
	delete Main.ClanDatePlayer where PlayerId = @id;
	delete [Current].ClanPlayer where PlayerId = @id;
	delete Main.Player where PlayerId = @id;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE proc [Tanks].[CalculateWn8ConsoleExpectedFromXvm]
(
	@date Date = null,
	@version varchar(15) = null
)
as
begin
	-- 2020-07-26: Migrates WN8 info from PC to Console

	set nocount on;

	begin tran CalcXvmWn8;
	
	-- determina o mais atual
	declare @source varchar(15);
	set @source = 'XVM';

	if ((@date is null) or (@version is null))
	begin

		select top (1) @date = [Date], @version = [Version]
			from Tanks.Wn8ExpectedLoad 
			where [Source] = 'XVM'
			order by Moment desc;
	end;

	--print @date;
	--print @source;
	--print @version;

	-- erases previous data
	delete Tanks.Wn8Expected
		where [Date] = @date and [Source] = @source and [Version] = @version;

	-- Copy to Console when same Id, Tier and Nation and not on the exclusion list or surrogate list
	-- Not Tier because WG Console finds that some tanks need a new class...
	insert into Tanks.Wn8Expected ([Date], [Source], [Version], TankId, Def, Frag, Spot, Damage, WinRate, Origin)
		select xvm.[Date], xvm.[Source], xvm.[Version], xvm.TankId, xvm.Def, xvm.Frag, xvm.Spot, xvm.Damage, xvm.WinRate, 0 as Origin
			from Tanks.Wn8PcExpected xvm
				inner join Tanks.PcTank pt
					on pt.TankId = xvm.TankId
				inner join Tanks.Tank t
					on (t.TankId = pt.TankId) and (t.NationId = pt.NationId) and (t.Tier = pt.Tier)
			where 
				(xvm.[Date] = @date) and (xvm.[Source] = @source) and (xvm.[Version] = @version)
				and pt.TankId not in (select TankId from Tanks.Wn8XvmExclusion)
				and t.TankId not in (select TankId from Tanks.Wn8XvmSurrogate)
				and (t.TankId not in (select TankId from Tanks.Wn8Expected where [Date] = @date and [Source] = @source and [Version] = @version));

	-- Copy to Console when different Id, but same Short Name, Tier and Nation and not on the exclusion list or surrogate list
	insert into Tanks.Wn8Expected ([Date], [Source], [Version], TankId, Def, Frag, Spot, Damage, WinRate, Origin)
		select xvm.[Date], xvm.[Source], xvm.[Version], t.TankId, xvm.Def, xvm.Frag, xvm.Spot, xvm.Damage, xvm.WinRate, 0 as Origin
			from Tanks.Wn8PcExpected xvm
				inner join Tanks.PcTank pt
					on pt.TankId = xvm.TankId
				inner join Tanks.Tank t
					on (t.ShortName = pt.ShortName) and (t.NationId = pt.NationId) and (t.Tier = pt.Tier)
			where 
				(xvm.[Date] = @date) and (xvm.[Source] = @source) and (xvm.[Version] = @version)
				and pt.TankId not in (select TankId from Tanks.Wn8XvmExclusion)
				and t.TankId not in (select TankId from Tanks.Wn8XvmSurrogate)
				and (t.TankId not in (select TankId from Tanks.Wn8Expected where [Date] = @date and [Source] = @source and [Version] = @version));

	-- surrogates from 2 PC Tanks (the average)
	insert into Tanks.Wn8Expected ([Date], [Source], [Version], TankId, Def, Frag, Spot, Damage, WinRate, Origin)
		select @date, @source, @version, t.TankId, (se.Def + se2.Def)/2.0, (se.Frag + se2.Frag)/2.0, (se.Spot + se2.Spot)/2.0, (se.Damage + se2.Damage)/2.0, (se.WinRate + se2.WinRate)/2.0, 3 as Origin
			from Tanks.Tank t
				inner join Tanks.Wn8XvmSurrogate s
					on s.TankId = t.TankId				
				inner join Tanks.Wn8PcExpected se
					on se.[Date] = @date and se.[Source] = @source and se.[Version] = @version and se.TankId = s.SurrogateTankId
				inner join Tanks.Wn8PcExpected se2
					on se2.[Date] = @date and se2.[Source] = @source and se2.[Version] = @version and se2.TankId = s.SecondSurrogateTankId
			where (s.SecondSurrogateTankId is not null) 
				and (t.TankId not in (select TankId from Tanks.Wn8Expected where [Date] = @date and [Source] = @source and [Version] = @version));

	-- Surrogates from PC
	insert into Tanks.Wn8Expected ([Date], [Source], [Version], TankId, Def, Frag, Spot, Damage, WinRate, Origin)
		select @date, @source, @version, t.TankId, se.Def, se.Frag, se.Spot, se.Damage, se.WinRate, 1 as Origin
			from Tanks.Tank t
				inner join Tanks.Wn8XvmSurrogate s
					on s.TankId = t.TankId
				inner join Tanks.Wn8PcExpected se
					on se.[Date] = @date and se.[Source] = @source and se.[Version] = @version and se.TankId = s.SurrogateTankId
			where t.TankId not in (select TankId from Tanks.Wn8Expected where [Date] = @date and [Source] = @source and [Version] = @version);


	-- Aplicar a relação aos valores do XBOX para completar os que ainda faltam, mas só se tiver boa amostragem no console
	insert into Tanks.Wn8Expected ([Date], [Source], [Version], TankId, Def, Frag, Spot, Damage, WinRate, Origin)
		select @date, @source, @version, t.TankId, a.DroppedCapturePoints/r.DefRel as Def, a.Kills/r.FragRel as Frag,
			a.Spotted/r.SpotRel as Spot, a.DamageDealt/r.DamageRel as Damage, a.WinRatio/r.WinRateRel as WinRate, 2 as Origin
				from Tanks.Tank t
					inner join Tanks.CurrentAvgWn8RelByTierType r
						on r.Tier = t.Tier and r.TypeId = t.TypeId
					inner join Performance.TanksAll a
						on a.TankId = t.TankId
				where t.TankId not in (select TankId from Tanks.Wn8Expected where [Date] = @date and [Source] = @source and [Version] = @version)
					and a.TotalPlayers >= 1000;

	-- Usar a média do tier/tipo
	insert into Tanks.Wn8Expected ([Date], [Source], [Version], TankId, Def, Frag, Spot, Damage, WinRate, Origin)
		select @date, @source, @version, t.TankId, r.Def, r.Frag, r.Spot, r.Damage, r.WinRate, 4 as Origin
				from Tanks.Tank t
					inner join Tanks.CurrentWn8ExpectedByTierType r
						on r.Tier = t.Tier and r.TypeId = t.TypeId					
				where t.TankId not in (select TankId from Tanks.Wn8Expected where [Date] = @date and [Source] = @source and [Version] = @version);
					
	commit tran CalcXvmWn8;

end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO




CREATE proc [Tanks].[SetPcTank]
(
	@tankId bigint,
	@name nvarchar(50),
	@shortName nvarchar(15),
	@nationId int,
	@tier int,
	@typeId int,
	@tag nvarchar(50),
	@isPremium bit
)
as
begin
	set nocount on;

	begin tran SetTank;

	update [Tanks].[PcTank]
			set 
				[Name] = @name,
				[ShortName] = @shortName,
				[NationId] = @nationId,
				[Tier] = @tier,
				[TypeId] = @typeId,
				[Tag] = @tag,
				IsPremium = @isPremium
			where ([TankId] = @tankId);

	if @@ROWCOUNT <= 0
	begin
		insert into [Tanks].[PcTank] (TankId, [Name], ShortName, NationId, Tier, TypeId, Tag, IsPremium)
			values (@tankId, @name, @shortName, @nationId, @tier, @typeId, @tag, @isPremium);
	end;
	
	commit tran SetTank;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO



CREATE proc [Tanks].[SetTank]
(
	@tankId bigint,
	@name nvarchar(50),
	@shortName nvarchar(15),
	@nationId int,
	@tier int,
	@typeId int,
	@tag nvarchar(50),
	@isPremium bit
)
as
begin
	set nocount on;

	begin tran SetTank;

	update [Tanks].[Tank]
			set 
				[Name] = @name,
				[ShortName] = @shortName,
				[NationId] = @nationId,
				[Tier] = @tier,
				[TypeId] = @typeId,
				[Tag] = @tag,
				IsPremium = @isPremium
			where ([TankId] = @tankId);

	if @@ROWCOUNT <= 0
	begin
		insert into [Tanks].[Tank] (TankId, [Name], ShortName, NationId, Tier, TypeId, Tag, IsPremium)
			values (@tankId, @name, @shortName, @nationId, @tier, @typeId, @tag, @isPremium);
	end;
	
	commit tran SetTank;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE proc [Tanks].[SetWn8ExpectedLoad]
(
	@date date,
	@source varchar(15),
	@version varchar(15),
	@moment datetime
)
as
begin
	set nocount on;

	begin tran SetWn8ExpectedLoad;

	update Tanks.Wn8ExpectedLoad 
		set Moment = @moment
		where ([Date] = @date) and ([Source] = @source) and ([Version] = @version);

	if (@@ROWCOUNT <= 0)
	begin
		insert into Tanks.Wn8ExpectedLoad ([Date], [Source], [Version], Moment) values (@date, @source, @version, @moment);
	end
	
	commit tran SetWn8ExpectedLoad;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
create proc [Tanks].[SetWn8PcExpectedValues]
(
	@date date,
	@source varchar(15),
	@version varchar(15),
	@tankId bigint,
	@def float,
	@frag float,
	@spot float,
	@damage float,
	@winRate float
)
as
begin
	-- 2020-07-26: Populating only the PC Expected Table
	set nocount on;

	begin tran SetWn8ExpectedValues;

	update Tanks.Wn8PcExpected
			set 
				Def = @def, Frag = @frag, Spot = @spot, Damage = @damage, WinRate = @winRate
			where ([Date] = @date) and ([Source] = @source) and ([Version] = @version) and (TankId = @tankId);

	if @@ROWCOUNT <= 0
	begin
		insert into Tanks.Wn8PcExpected ([Date], [Source], [Version], TankId, Def, Frag, Spot, Damage, WinRate) 
			values (@date, @source, @version, @tankId, @def, @frag, @spot, @damage, @winRate);
	end;
	
	commit tran SetWn8ExpectedValues;
end;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

create   trigger [Main].[RecordClanTagChange] on [Main].[Clan]
for insert, update, delete
as
begin
	set nocount on;

	if UPDATE(ClanTag)
	begin
		--print 'ClanTag change...'

		-- Os atualizados
		insert into Main.ClanTagChange (ClanId, Moment, PreviousClanTag, CurrentClanTag)
			select i.ClanId, GETUTCDATE(), d.ClanTag, i.ClanTag 
				from inserted i
					inner join deleted d
						on i.ClanId = d.ClanId
				where i.ClanTag <> d.ClanTag;

		-- Os adicionados
		insert into Main.ClanTagChange (ClanId, Moment, PreviousClanTag, CurrentClanTag)
			select i.ClanId, GETUTCDATE(), null, i.ClanTag 
				from inserted i
				where i.ClanId not in (select ClanId from deleted);
	end;

	-- Os deletados
	insert into Main.ClanTagChange (ClanId, Moment, PreviousClanTag, CurrentClanTag)
		select d.ClanId, GETUTCDATE(), d.ClanTag, null 
			from deleted d
			where d.ClanId not in (select ClanId from inserted);

end;
GO
ALTER TABLE [Main].[Clan] ENABLE TRIGGER [RecordClanTagChange]
GO
