using System.Collections.Generic;

namespace GoolsDev.App.Cosmos
{
    public record NflPlayerStatsDocument(
        string Id,
        string PlayerId,
        int Year,
        string TeamId,
        string TeamShortName,
        string FirstName,
        string LastName,
        string JerseyNumber,
        string PositionId,
        string Position,
        List<NflPlayerStatline> Statlines
    );

    public record NflPlayerStatline
    (
        int PassingYards,
        int PassingTouchdowns,
        int Interceptions,
        int RushingYards,
        int RushingTouchdowns,
        int Receptions,
        int ReceivingYards,
        int ReceivingTouchdowns,
        int ReturnTouchdowns,
        int FumblesLost,
        int Week,
        int SeasonType,
        double FantasyPoints
    );
}
