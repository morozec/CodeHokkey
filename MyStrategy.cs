using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk
{
    internal enum PointInPolygonPosition
    {
        Inside,
        Outside,
        Boundary
    };

    internal enum PointOnEdgePosition
    {
        Left,
        Right,
        Beyond,
        Behind,
        Between,
        Origin,
        Destination
    }

    internal enum EdgeType
    {
        Touching,
        Crossing,
        Inessential
    };


    public sealed class MyStrategy : IStrategy
    {
        enum Position
        {
            Back,
            HalfBack,
            Forward
        }

        private class Point
        {
            public double X { get; set; }
            public double Y { get; set; }

            public Point(double x, double y)
            {
                X = x;
                Y = y;
            }

            public double Lenght
            {
                get
                {
                    return Math.Sqrt(X * X + Y * Y);
                }
            }

            public PointOnEdgePosition Classify(Point p0, Point p1)
            {
                var a = p1 - p0;
                var b = this - p0;
                var sa = a.X * b.Y - b.X * a.Y;
                if (sa > 0) return PointOnEdgePosition.Left;
                if (sa < 0.0) return PointOnEdgePosition.Right;
                if ((a.X * b.X < 0.0) || (a.Y * b.Y < 0.0)) return PointOnEdgePosition.Behind;
                if (a.Lenght < b.Lenght)
                    return PointOnEdgePosition.Beyond;
                if (p0 == this)
                    return PointOnEdgePosition.Origin;
                if (p1 == this)
                    return PointOnEdgePosition.Destination;
                return PointOnEdgePosition.Between;

            }

            public PointOnEdgePosition Classify(Edge e)
            {
                return Classify(e.Org, e.Dest);
            }

            public static Point operator -(Point p1, Point p2)
            {
                return new Point(p1.X - p2.X, p1.Y - p2.Y);
            }

            public static bool operator ==(Point p1, Point p2)
            {
                if (p1 == null && p2 == null)
                    return true;
                if (p1 == null && p2 != null)
                    return false;
                if (p1 != null && p2 == null)
                    return false;
                return p1.X == p2.X && p1.Y == p2.Y;
            }

            public static bool operator !=(Point p1, Point p2)
            {
                return !(p1 == p2);
            }
        }

        private class Edge
        {
            public Point Org { get; set; }
            public Point Dest { get; set; }

            public Edge(Point org, Point dest)
            {
                Org = org;
                Dest = dest;
            }
        }

        private class Polygon
        {
            public IList<Edge> Edges { get; set; }

            public Polygon(IList<Edge> edges)
            {
                Edges = edges;
            }
        }

        private static class Helper
        {
            public static EdgeType GetEdgeType(Point a, Edge e)
            {
                var v = e.Org;
                var w = e.Dest;

                switch (a.Classify(e))
                {
                    case PointOnEdgePosition.Left:
                        return ((v.Y < a.Y) && (a.Y <= w.Y)) ? EdgeType.Crossing : EdgeType.Inessential;
                    case PointOnEdgePosition.Right:
                        return ((w.Y < a.Y) && (a.Y <= v.Y)) ? EdgeType.Crossing : EdgeType.Inessential;
                    case PointOnEdgePosition.Between:
                    case PointOnEdgePosition.Origin:
                    case PointOnEdgePosition.Destination:
                        return EdgeType.Touching;
                    default:
                        return EdgeType.Inessential;

                }
            }

            public static PointInPolygonPosition GetPointInPoligonPosition(Point a, Polygon p)
            {
                var parity = 0;
                for (int i = 0; i < p.Edges.Count; ++i)
                {
                    var e = p.Edges[i];
                    switch (Helper.GetEdgeType(a, e))
                    {
                        case EdgeType.Touching: return PointInPolygonPosition.Boundary;
                        case EdgeType.Crossing:
                            parity = 1 - parity;
                            break;
                    }
                }
                return (parity != 0 ? PointInPolygonPosition.Inside : PointInPolygonPosition.Outside);
            }
        }

        //private class Square
        //{
        //    public double Left { get; set; }
        //    public double Right { get; set; }
        //    public double Bottom { get; set; }
        //    public double Top { get; set; }

        //    public Square(double left, double right, double bottom, double top)
        //    {
        //        Left = left;
        //        Right = right;
        //        Bottom = bottom;
        //        Top = top;
        //    }
        //}
        private const double PuckSpeedLoss = 0.02;

        private const double StrikeAngle = 1.0D * Math.PI / 180.0D;
        private const double EpsDefencePointDist = 40d;
        private const double ShootingSquareSide = 210d;
        private const double EpsSpeed = 0.01;
        private const double EpsAngle = 0.001;

        private const double AcceptableDistToOpponentHockeyistToGo = 550d;
      
        private const double ShootingPointXShift = 210;

        private const double DistToShootingPointWhenNeedTurn = 400d;
        private const double DistFromOppHockeysitToNetWhenNeedTurn = 350d;


        private const double BorderZoneHeight = 150d;

        private const double DistanceFromCenterToStartMeetOpponentHockeyistWithPuck = 200;

        public const double DistWhenNeadTurn = 250d;

        private const double CriticalPuckSpeed = 20d;

        private const double CenterY = 460;
        private const double TopY = 150;
        private const double BottomY = 770;
        private const double LeftX = 65;
        private const double RightX = 1135;

        private const double CancelShootingAngleError = 3 * Math.PI / 180.0D;

        private const int DeltaTime = 1;

        private const double DistToMyNetCornerWhenIShouldNotStrike = 400;

        public void Move(Hockeyist self, World world, Game game, Move move)
        {
            var myNetIsLeft = world.GetMyPlayer().NetFront < world.GetOpponentPlayer().NetFront;

            if (world.Puck.OwnerPlayerId == self.PlayerId)
            {
                #region Шайба у меня

                if (world.Puck.OwnerHockeyistId == self.Id)
                {

                    #region Игрок, владеющий шайбой

                    //if (world.GetMyPlayer().GoalCount == 0 && world.GetOpponentPlayer().GoalCount == 0 &&
                    //    world.Tick > world.TickCount &&
                    //    !world.GetMyPlayer().IsJustMissedGoal && !world.GetMyPlayer().IsJustScoredGoal)
                    //{
                    //    #region овертайм без вратарей

                    //    var oppNetCenter = GetNetCenter(world.GetOpponentPlayer());

                    //    move.SpeedUp = 0;
                    //    if (Math.Abs(self.GetAngleTo(oppNetCenter.X, oppNetCenter.Y)) < StrikeAngle)
                    //    {
                    //        move.Action = ActionType.Strike;
                    //    }
                    //    else
                    //    {
                    //        move.Turn = self.GetAngleTo(oppNetCenter.X, oppNetCenter.Y);
                    //        move.Action = ActionType.None;
                    //    }

                    //    return;
                    //    #endregion
                    //}


                    var isOverTime = world.GetMyPlayer().GoalCount == 0 && world.GetOpponentPlayer().GoalCount == 0 &&
                                     world.Tick > world.TickCount &&
                                     !world.GetMyPlayer().IsJustMissedGoal && !world.GetMyPlayer().IsJustScoredGoal;


                    if (isOverTime)
                    {
                        move.SpeedUp = 1d;
                        move.Turn = self.GetAngleTo(world.GetOpponentPlayer().NetFront,
                                                    (world.GetOpponentPlayer().NetTop +
                                                     world.GetOpponentPlayer().NetBottom)/2);

                        var angleToOppTop = self.GetAngleTo(world.GetOpponentPlayer().NetFront,
                                                            world.GetOpponentPlayer().NetTop);
                        var angleToOppBottom = self.GetAngleTo(world.GetOpponentPlayer().NetFront,
                                                            world.GetOpponentPlayer().NetBottom);

                        var canStrike = angleToOppTop*angleToOppBottom < 0 && Math.Abs(angleToOppTop) < Math.PI/2 &&
                                        Math.Abs(angleToOppBottom) < Math.PI/2 &&
                                        Math.Abs(angleToOppBottom) >= CancelShootingAngleError &&
                                        Math.Abs(angleToOppTop) >= CancelShootingAngleError;

                        if (canStrike)
                        {
                            move.Action = self.SwingTicks > 0 ? ActionType.Strike : ActionType.Swing;
                        }

                        return;
                    }

                    move.SpeedUp = 1d;
                    
                 

                    var opponentPlayer = world.GetOpponentPlayer();

                    var shootingPoint = GetShootingPoint(world, self);
                    //var shootingSquare = GetShootingSquare(shootingPoint, world);
                    //var selfPosition = Helper.GetPointInPoligonPosition(new Point(self.X, self.Y), shootingSquare);
                    //var isInShootingSquare = selfPosition == PointInPolygonPosition.Boundary || selfPosition ==
                    //                         PointInPolygonPosition.Inside;

                    var swingTicks = GetSwingTickToStrikeFromPoint(self, world, game);

                    double netX = 0.5D * (opponentPlayer.NetBack + opponentPlayer.NetFront);
                    double netY = 0.5D * (opponentPlayer.NetBottom + opponentPlayer.NetTop);
                    netY += (self.Y < netY ? 0.5D : -0.5D) * game.GoalNetHeight;

                    double angleToNet = self.GetAngleTo(netX, netY);

                    if (swingTicks != -1)
                    {
                        

                      
                        move.Turn = angleToNet;


                        if (Math.Abs(angleToNet) < StrikeAngle && Math.Abs(self.X - world.GetOpponentPlayer().NetFront) < 600)
                        {
                            var timeToShootingPoint = GetTimeToPoint(self, shootingPoint.X, shootingPoint.Y, world, game,
                                                                     false, true);

                            var canBetMet = OppenentHockeyistsCanReachMeMovingForward(self, timeToShootingPoint,
                                                                                      game.HockeyistSpeedUpFactor, world,
                                                                                      game);
                            if (canBetMet)
                            {

                                var selfSpeed = Math.Sqrt(self.SpeedX*self.SpeedX + self.SpeedY*self.SpeedY);
                                var speedAngle = GetSpeedAngle(self.SpeedX, self.SpeedY);
                                var speedUp = GetSpeedUpToStop(selfSpeed * (self.Angle * speedAngle > 0 ? 1 : -1), game);
                                move.SpeedUp = speedUp;

                                if (self.SwingTicks < swingTicks)
                                {
                                    int timeToAceptableSwingTicks;
                                    double realSpeedUp;
                                    if (self.State != HockeyistState.Swinging)
                                    {
                                        realSpeedUp = speedUp;
                                        timeToAceptableSwingTicks = Math.Max(swingTicks, game.SwingActionCooldownTicks);
                                    }
                                    else
                                    {
                                        realSpeedUp = 0d;
                                        timeToAceptableSwingTicks = swingTicks - self.SwingTicks;
                                    }
                                    //var speedXAfterSpeedUp = self.SpeedX + realSpeedUp*Math.Cos(self.Angle)*1;
                                    //var speedYAfterSpeedUp = self.SpeedY + realSpeedUp*Math.Sin(self.Angle)*1;

                                    //var xAfterAcceptableSwingTicks = self.X + self.SpeedX * 1 + realSpeedUp * Math.Cos(self.Angle) * 1 * 1 /2;
                                    //var yAfterAcceptableSwingTicks = self.Y + self.SpeedY * 1 + realSpeedUp * Math.Sin(self.Angle) * 1 * 1 / 2;

                                    //if (timeToAceptableSwingTicks > 1)
                                    //{
                                    //    xAfterAcceptableSwingTicks += speedXAfterSpeedUp*(timeToAceptableSwingTicks - 1);
                                    //    yAfterAcceptableSwingTicks += speedYAfterSpeedUp*(timeToAceptableSwingTicks - 1);
                                    //}
                                    var xAfterAcceptableSwingTicks = self.X + self.SpeedX * timeToAceptableSwingTicks;
                                    var yAfterAcceptableSwingTicks = self.Y + self.SpeedY * timeToAceptableSwingTicks;


                                    var bigAngle =
                                        Math.Atan(Math.Abs(netY - yAfterAcceptableSwingTicks) /
                                                  Math.Abs(netX - xAfterAcceptableSwingTicks));
                                    double correctedSelfAngle;
                                    if (self.Angle >= 0)
                                    {
                                        correctedSelfAngle = Math.Min(self.Angle, Math.PI - self.Angle);
                                    }
                                    else
                                    {
                                        correctedSelfAngle = Math.Min(Math.Abs(self.Angle), Math.PI - Math.Abs(self.Angle));
                                    }
                                    var newAngleToNetCorner = correctedSelfAngle - bigAngle;

                                    if (Math.Abs(newAngleToNetCorner) < StrikeAngle)
                                        move.Action = ActionType.Swing;
                                }
                                else
                                {
                                    move.Action = ActionType.Strike;
                                }
                            }
                            else
                            {
                                if (Math.Abs(self.X - world.GetOpponentPlayer().NetFront) <
                                    Math.Abs(shootingPoint.X - world.GetOpponentPlayer().NetFront))
                                {
                                    move.Action = ActionType.Strike;
                                }
                            }

                            //var puckSpeed = GetPuckAfterStrikeSpeed(self, game, 0);

                            //var canInterceptPuck = CanInterceptPuck(world.Puck.X, world.Puck.Y, puckSpeed, self.Angle,
                            //                                        world, game);


                            //var isMaxSwingTicks = self.SwingTicks >= game.MaxEffectiveSwingTicks;


                            //var angleToXAxis = self.GetAngleTo(world.Width, self.X);
                            //var speedUp = self.State == HockeyistState.Swinging ? 0 : game.HockeyistSpeedUpFactor;
                            //var hasCloseOpps = OppenentHockeyistsCanReachMeMovingForward(self, 1,
                            //                                                                 speedUp,
                            //                                                                 world, game);
                            //if (!canInterceptPuck)
                            //{

                            //    var newX = self.X + self.SpeedX * 1 +
                            //               speedUp * Math.Cos(angleToXAxis) * 1 * 1 / 2;
                            //    var newY = self.Y + self.SpeedY * 1 +
                            //               speedUp * Math.Sin(angleToXAxis) * 1 * 1 / 2;

                            //    var newPosition = Helper.GetPointInPoligonPosition(new Point(newX, newY), shootingSquare);
                            //    var isInSquare = newPosition == PointInPolygonPosition.Boundary || newPosition ==
                            //                     PointInPolygonPosition.Inside;


                            //    if (!isInSquare || hasCloseOpps || isMaxSwingTicks)
                            //    {
                            //        move.Action = ActionType.Strike;
                            //    }
                            //    else
                            //    {
                            //        move.Action = ActionType.None;
                            //    }
                            //}
                            //else
                            //{
                            //    var newX = self.X + self.SpeedX * 1 +
                            //               speedUp * Math.Cos(angleToXAxis) * 1 * 1 / 2;
                            //    var newY = self.Y + self.SpeedY * 1 +
                            //               speedUp * Math.Sin(angleToXAxis) * 1 * 1 / 2;

                            //    var newPosition = Helper.GetPointInPoligonPosition(new Point(newX, newY), shootingSquare);
                            //    var isInSquare = newPosition == PointInPolygonPosition.Boundary || newPosition ==
                            //                     PointInPolygonPosition.Inside;

                            //    if (!isInSquare || isMaxSwingTicks)
                            //    {
                            //        move.Action = ActionType.Strike;
                            //    }
                            //    else
                            //    {
                            //        if (self.State == HockeyistState.Swinging)
                            //        {
                            //            move.Action = hasCloseOpps ? ActionType.Strike : ActionType.None;
                            //        }
                            //        else
                            //        {
                            //            var intercetionPoint =
                            //                GetEdgeAndPolygonIntersectionPoint(
                            //                    new Edge(new Point(self.X, self.Y), new Point(netX, netY)),
                            //                    shootingSquare);

                            //            var distToBorder = self.GetDistanceTo(intercetionPoint.X, intercetionPoint.Y);
                            //            var speed = Math.Sqrt(self.SpeedX * self.SpeedX + self.SpeedY * self.SpeedY);

                            //            move.SpeedUp = 2 * (distToBorder - speed * game.SwingActionCooldownTicks) /
                            //                          (game.SwingActionCooldownTicks * game.SwingActionCooldownTicks);


                            //            move.Action = ActionType.Swing;
                            //        }
                            //    }

                            //}


                        }

                        else
                        {
                            //if (self.State == HockeyistState.Swinging)
                            //    move.Action = ActionType.Strike;
                            if (self.State == HockeyistState.Swinging)
                            {
                                move.Action = self.SwingTicks >= swingTicks ? ActionType.Strike : ActionType.Swing;
                            }
                        }

                    }
                    else
                    {
                        var isFatherThenShootingPoint = Math.Abs(self.X - world.GetOpponentPlayer().NetFront) <
                                                        Math.Abs(shootingPoint.X - world.GetOpponentPlayer().NetFront);

                        if (isFatherThenShootingPoint)
                        {
                            #region Игрок находится дальше точки броска. Пытаемся отдать назад

                            move.Turn = self.GetAngleTo(
                                world.GetMyPlayer().NetBack,
                                (world.GetMyPlayer().NetTop + world.GetMyPlayer().NetBottom)/2);
                            move.SpeedUp = 1d;

                            //TODO: переписать проверку на возможность отдать пас
                            double angleToMyBottomCorner;
                            double angleToMyTopCorner;
                            var canMakeBackPas = false;
                            var passAngle = 0d;
                            if (myNetIsLeft)
                            {
                                angleToMyBottomCorner = self.GetAngleTo(0, BottomY);
                                angleToMyTopCorner = self.GetAngleTo(0, TopY);


                                if (angleToMyBottomCorner <= 0 && angleToMyTopCorner >= 0)
                                {
                                    canMakeBackPas = true;
                                    passAngle = 0d;
                                }
                                else if (angleToMyTopCorner <= 0 && Math.Abs(angleToMyTopCorner) < game.PassSector*0.5)
                                {
                                    canMakeBackPas = true;
                                    passAngle = -game.PassSector*0.5;
                                }
                                else if (angleToMyBottomCorner >= 0 && angleToMyBottomCorner < game.PassSector*0.5)
                                {
                                    canMakeBackPas = true;
                                    passAngle = game.PassSector*0.5;
                                }
                            }
                            else
                            {
                                angleToMyBottomCorner = self.GetAngleTo(world.Width, BottomY);
                                angleToMyTopCorner = self.GetAngleTo(world.Width, TopY);
                                if (angleToMyBottomCorner >= 0 && angleToMyTopCorner <= 0)
                                {
                                    canMakeBackPas = true;
                                    passAngle = 0d;
                                }
                                else if (angleToMyTopCorner >= 0 && angleToMyTopCorner < game.PassSector*0.5)
                                {
                                    canMakeBackPas = true;
                                    passAngle = game.PassSector*0.5;
                                }
                                else if (angleToMyBottomCorner <= 0 &&
                                         Math.Abs(angleToMyBottomCorner) < game.PassSector*0.5)
                                {
                                    canMakeBackPas = true;
                                    passAngle = -game.PassSector*0.5;
                                }
                            }

                            //var xFinish = myNetIsLeft ? 0 : world.Width;
                            //var yFinish = self.Y + Math.Abs(self.X - xFinish)*Math.Tan(self.Angle);

                            var selfSpeed = Math.Sqrt(self.SpeedX * self.SpeedX + self.SpeedY * self.SpeedY);
                            var speedAngle = GetSpeedAngle(self.SpeedX, self.SpeedY);
                            var puckSpeed = 15.0 * 1 +
                                            selfSpeed * Math.Cos(self.Angle - speedAngle);


                            var canInterceptPuck = CanInterceptPuck(world.Puck.X, world.Puck.Y, puckSpeed, self.Angle + passAngle,
                                                                    world, game);

                            if (canMakeBackPas && !canInterceptPuck)
                            {
                                move.PassPower = 1d;
                                move.PassAngle = passAngle;
                                move.Action = ActionType.Pass;
                            }

                            if (self.State == HockeyistState.Swinging)
                            {
                                move.Action = ActionType.CancelStrike;
                            }

                            #endregion
                        }
                        else
                        {
                            #region Игрок не дальше области броска

                            var farFromShootingPoint =
                                self.GetDistanceTo(shootingPoint.X, shootingPoint.Y) >
                                DistToShootingPointWhenNeedTurn;


                            var isInTopBorderZone = self.Y - TopY < BorderZoneHeight;
                            var isInBottomBorderZone = BottomY - self.Y < BorderZoneHeight;
                            var isInBorderZone = isInTopBorderZone || isInBottomBorderZone;

                            var nearestBeforeOpponent = GetNearestBeforeOpponent(self, world);

                            if (nearestBeforeOpponent != null &&
                                Math.Abs(nearestBeforeOpponent.X - world.GetOpponentPlayer().NetFront) >
                                Math.Abs(shootingPoint.X - world.GetOpponentPlayer().NetFront))
                            {
                                #region Перед игроком есть соперник, он достаточно близко и есть возможность повернуть

                                if (isInBorderZone)
                                {
                                    #region Игрок в граничной зоне

                                    var oppInBorderZone =
                                        nearestBeforeOpponent.GetDistanceTo(nearestBeforeOpponent.X, self.Y) <
                                        self.GetDistanceTo(nearestBeforeOpponent.X, self.Y);

                                    if (oppInBorderZone)
                                    {

                                        var yTurn = self.Y - TopY < BorderZoneHeight
                                                        ? BottomY - self.Radius
                                                        : TopY + self.Radius;
                                        move.Turn = self.GetAngleTo(self.X, yTurn);

                                    }
                                    else
                                    {
                                        if (farFromShootingPoint)
                                        {
                                            move.Turn = self.GetAngleTo(
                                                world.GetOpponentPlayer().NetFront,
                                                isInTopBorderZone
                                                    ? TopY + self.Radius
                                                    : BottomY - self.Radius);
                                        }
                                        else
                                        {
                                            move.Turn = self.GetAngleTo(shootingPoint.X, shootingPoint.Y);
                                        }

                                    }

                                    #endregion
                                }
                                else
                                {
                                    #region Игрок вне граничной зоны

                                    var y = Math.Abs(self.Y - TopY) >= Math.Abs(nearestBeforeOpponent.Y - TopY)
                                                ? BottomY
                                                : TopY;
                                    var destPoint = new Point(nearestBeforeOpponent.X, y);

                                    var myTime = GetTimeToPoint(self, destPoint.X, destPoint.Y, world, game, true, true);
                                    var oppTime = GetTimeToPoint(nearestBeforeOpponent, destPoint.X, destPoint.Y, world,
                                                                 game, true, true);



                                    if (myTime < oppTime)
                                        move.Turn = self.GetAngleTo(destPoint.X, destPoint.Y);
                                    else
                                    {
                                        double angle = self.GetAngleTo(self.X, destPoint.Y);
                                        //if (myNetIsLeft && y == BottomY)
                                        //    angle = angle < 0 ? angle : angle - 2*Math.PI;
                                        //else if (myNetIsLeft && y == TopY)
                                        //    angle = angle > 0 ? angle : 2*Math.PI + angle;
                                        //else if (!myNetIsLeft && y == BottomY)
                                        //    angle = angle > 0 ? angle : 2*Math.PI + angle;
                                        //else
                                        //    angle = angle < 0 ? angle : angle - 2*Math.PI;

                                        move.Turn = angle;
                                    }

                                    //if (Math.Abs(self.Y - TopY) >= Math.Abs(nearestBeforeOpponent.Y - TopY))
                                    //{
                                    //    move.Turn = self.GetAngleTo( self.X, BottomY);
                                    //}
                                    //else 
                                    //{
                                    //    move.Turn = self.GetAngleTo( self.X /2, TopY);
                                    //}


                                    #endregion
                                }

                                #endregion
                            }
                            else
                            {
                                #region Нет необходимости (или возможности) поворачивать из-за игрока соперника

                                if (!farFromShootingPoint && isInBorderZone)
                                {
                                    move.Turn = self.GetAngleTo(shootingPoint.X, shootingPoint.Y);
                                }
                                else
                                {
                                    var newX = (shootingPoint.X + self.X)/2;
                                    var newY = (shootingPoint.Y < CenterY)
                                                   ? TopY + self.Radius
                                                   : BottomY - self.Radius;
                                    move.Turn = self.GetAngleTo(newX, newY);
                                }

                                #endregion
                            }

                            if (self.State == HockeyistState.Swinging)
                            {
                                move.Action = ActionType.CancelStrike;
                            }

                            #endregion
                        }
                    }
                   

                    if (Math.Abs(angleToNet) > CancelShootingAngleError && self.State == HockeyistState.Swinging)
                    {
                        move.Action = ActionType.CancelStrike;
                    }

                    #endregion
                }

                else
                {
                    #region Игрок, не владеющий шайбой

                    var position = GetPositions(world)[self.Id];
                    var playerWithPuckPosition = GetPositions(world)[world.Puck.OwnerHockeyistId];

                    if (position == Position.Back ||
                        position == Position.HalfBack && playerWithPuckPosition == Position.Back)
                    {
                        #region Ближайший к моим воротам игрок без шайбы
                        var nearestToPuckOpponent = GetNearestToPuckOpponent(world);
                        if (nearestToPuckOpponent != null &&
                            self.GetDistanceTo(nearestToPuckOpponent) < world.Width/4)
                        {
                            AttackOpponentHockeyist(self, nearestToPuckOpponent, move, game, world, ActionType.Strike);
                        }
                        else
                        {
                            var defencePoint = GetDefencePoint(world);
                            if (IsInPoint(defencePoint.X, defencePoint.Y, self, world, EpsDefencePointDist))
                            {
                                var speed = Math.Sqrt(self.SpeedX*self.SpeedX + self.SpeedY*self.SpeedY);
                                var speedAngle = GetSpeedAngle(self.SpeedX, self.SpeedY);
                                move.SpeedUp = GetSpeedUpToStop(speed * (self.Angle * speedAngle > 0 ? 1 : -1), game); 
                                move.Turn = self.GetAngleTo(world.Puck);

                            }
                            else
                            {
                                MakeShortestMoveToPoint(defencePoint.X, defencePoint.Y, self, move, game, world);
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        #region Дальний от моих ворот игрок без шайбы 

                        Hockeyist nearestToOppNetOppHockeyist = null;
                        var minDist = double.MaxValue;
                        var oppNetCenter = GetNetCenter(world.GetOpponentPlayer());
                        foreach (var oppHockeyist in world.Hockeyists.Where(x=>!x.IsTeammate && x.Type != HockeyistType.Goalie))
                        {
                            var dist = oppHockeyist.GetDistanceTo(oppNetCenter.X, oppNetCenter.Y);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                nearestToOppNetOppHockeyist = oppHockeyist;
                            }
                        }

                        AttackOpponentHockeyist(self, nearestToOppNetOppHockeyist, move, game, world, ActionType.Strike);

                        #endregion

                    }
                 
                    if (self.State == HockeyistState.Swinging)
                    {
                        move.Action = ActionType.CancelStrike;
                    }

                    #endregion
                }

                #endregion
            }

            else
            {
                #region Шайба не у меня


                var position = GetPositions(world)[self.Id];

                if (position == Position.Back)
                {
                    #region Защитник

                    if (world.Hockeyists.All(x => x.Id != world.Puck.OwnerHockeyistId))
                    {
                        MoveWhenPuckIsFree(self, world, game, Position.Back, move);
                    }

                    else
                    {
                        #region Шайба у соперника

                        var oppHockeyist =
                            world.Hockeyists.SingleOrDefault(x => !x.IsTeammate && x.Id == world.Puck.OwnerHockeyistId);
                        if (oppHockeyist != null)
                        {
                            var puckSpeed =
                                Math.Sqrt(world.Puck.SpeedX*world.Puck.SpeedX + world.Puck.SpeedY*world.Puck.SpeedY);

                            var canTakePuck = puckSpeed < CriticalPuckSpeed;


                            var defencePoint = GetDefencePoint(world);
                            if (IsInPoint(defencePoint.X, defencePoint.Y, self, world, EpsDefencePointDist))
                            {
                                var speed = Math.Sqrt(self.SpeedX * self.SpeedX + self.SpeedY * self.SpeedY);
                                var speedAngle = GetSpeedAngle(self.SpeedX, self.SpeedY);
                                move.SpeedUp = GetSpeedUpToStop(speed * (self.Angle * speedAngle > 0 ? 1 : -1), game); 
                                move.Turn = self.GetAngleTo(world.Puck);

                                if (self.GetDistanceTo(world.Puck) <= game.StickLength &&
                                    Math.Abs(self.GetAngleTo(world.Puck)) < 0.5D*game.StickSector && !canTakePuck)
                                    move.Action = ActionType.Strike;
                                else
                                {
                                    move.Action = ActionType.TakePuck;
                                }
                            }
                            else
                            {
                                var myTimeToDefencePoint =
                                       Math.Min(
                                           GetTimeToPoint(self, defencePoint.X, defencePoint.Y, world, game, false,
                                                          true),
                                           GetTimeToPoint(self, defencePoint.X, defencePoint.Y, world, game, false,
                                                          false));
                                var dangerousPointX = myNetIsLeft
                                                          ? world.Width / 2 +
                                                            DistanceFromCenterToStartMeetOpponentHockeyistWithPuck
                                                          : world.Width / 2 -
                                                            DistanceFromCenterToStartMeetOpponentHockeyistWithPuck;

                                var oppTimeToDengerousPosition =
                                    Math.Min(
                                        GetTimeToPoint(oppHockeyist, dangerousPointX, TopY, world, game, false, true),
                                        GetTimeToPoint(oppHockeyist, dangerousPointX, BottomY, world, game, false,
                                                       true));

                                if (myTimeToDefencePoint > oppTimeToDengerousPosition)
                                {
                                    AttackOpponentHockeyist(self, oppHockeyist, move, game, world, ActionType.Strike);
                                }
                                else
                                {
                                    MakeShortestMoveToPoint(defencePoint.X, defencePoint.Y, self, move, game, world);
                                }
                            }
                        }  
                        #endregion
                    }

                  


                    #endregion

                }

                else if (position == Position.Forward)
                {
                    #region Нападающий


                    if (world.Hockeyists.All(x => x.Id != world.Puck.OwnerHockeyistId))
                    {
                        MoveWhenPuckIsFree(self, world, game, Position.Forward, move);
                    }

                    else
                    {
                        var oppHockeyist =
                            world.Hockeyists.Single(x => !x.IsTeammate && x.Id == world.Puck.OwnerHockeyistId);
                        AttackOpponentHockeyist(self, oppHockeyist, move, game, world, ActionType.Strike);
                    }

                    #endregion

                }

                else
                {
                    #region Полузащитник

                    if (world.Hockeyists.All(x => x.Id != world.Puck.OwnerHockeyistId))
                    {
                        MoveWhenPuckIsFree(self, world, game, Position.HalfBack, move);
                    }
                    else
                    {
                       
                        var oppHockeyist =
                            world.Hockeyists.SingleOrDefault(x => !x.IsTeammate && x.Id == world.Puck.OwnerHockeyistId);
                        if (oppHockeyist != null)
                        {
                            if (Math.Abs(oppHockeyist.X - world.GetMyPlayer().NetBack) < world.Width / 2 + DistanceFromCenterToStartMeetOpponentHockeyistWithPuck)
                            {
                                AttackOpponentHockeyist(self, oppHockeyist, move, game, world, ActionType.Strike);
                            }
                            else
                            {
                                var beforeDefencePoint = GetBeforeDefencePoint(world);
                                if (IsInPoint(beforeDefencePoint.X, beforeDefencePoint.Y, self, world,
                                              EpsDefencePointDist))
                                {
                                    var speed = Math.Sqrt(self.SpeedX * self.SpeedX + self.SpeedY * self.SpeedY);
                                    var speedAngle = GetSpeedAngle(self.SpeedX, self.SpeedY);
                                   move.SpeedUp = GetSpeedUpToStop(speed * (self.Angle * speedAngle > 0 ? 1 : -1), game); 
                                    move.Turn = self.GetAngleTo(world.Puck);
                                }
                                else
                                {
                                    var myTimeToBeforeDefencePoint =
                                       Math.Min(
                                           GetTimeToPoint(self, beforeDefencePoint.X, beforeDefencePoint.Y, world, game, false,
                                                          true),
                                           GetTimeToPoint(self, beforeDefencePoint.X, beforeDefencePoint.Y, world, game, false,
                                                          false));
                                    var dangerousPointX = myNetIsLeft
                                                              ? world.Width / 2 +
                                                                DistanceFromCenterToStartMeetOpponentHockeyistWithPuck
                                                              : world.Width / 2 -
                                                                DistanceFromCenterToStartMeetOpponentHockeyistWithPuck;

                                    var oppTimeToDengerousPosition =
                                        Math.Min(
                                            GetTimeToPoint(oppHockeyist, dangerousPointX, TopY, world, game, false, true),
                                            GetTimeToPoint(oppHockeyist, dangerousPointX, BottomY, world, game, false,
                                                           true));

                                    if (myTimeToBeforeDefencePoint > oppTimeToDengerousPosition)
                                    {
                                        AttackOpponentHockeyist(self, oppHockeyist, move, game, world, ActionType.Strike);
                                    }
                                    else
                                    {
                                        MakeShortestMoveToPoint(beforeDefencePoint.X, beforeDefencePoint.Y,
                                                                self, move, game, world);
                                    }
                                }
                            }
                        }
                    }

                    #endregion
                }

                if (self.State == HockeyistState.Swinging)
                {
                    move.Action = ActionType.CancelStrike;
                }


                #endregion

            }


        }

        private bool NotStrikeNearMyNet(Hockeyist myHockeyist, World world)
        {
            var notStrike = false;
            var myNetCornerX = (world.GetMyPlayer().NetLeft + world.GetMyPlayer().NetRight) / 2;
            var myNetTopCornerY = world.GetMyPlayer().NetTop;
            var myNetBottomCornerY = world.GetMyPlayer().NetBottom;

            if (myHockeyist.GetDistanceTo(myNetCornerX, myNetTopCornerY) < DistToMyNetCornerWhenIShouldNotStrike &&
                Math.Abs(myHockeyist.GetAngleTo(myNetCornerX, myNetTopCornerY)) <= CancelShootingAngleError)
                notStrike = true;

            if (myHockeyist.GetDistanceTo(myNetCornerX, myNetBottomCornerY) < DistToMyNetCornerWhenIShouldNotStrike &&
                Math.Abs(myHockeyist.GetAngleTo(myNetCornerX, myNetBottomCornerY)) <= CancelShootingAngleError)
                notStrike = true;

            return notStrike;
        }

        private bool StrikeNearOppNet(Hockeyist myHockeyist, World world)
        {
            var strike = false;
            var oppNetCornerX = (world.GetOpponentPlayer().NetLeft + world.GetOpponentPlayer().NetRight) / 2;
            var oppNetTopCornerY = world.GetOpponentPlayer().NetTop;
            var oppNetBottomCornerY = world.GetOpponentPlayer().NetBottom;

            if (myHockeyist.GetDistanceTo(oppNetCornerX, oppNetTopCornerY) < DistToMyNetCornerWhenIShouldNotStrike &&
                Math.Abs(myHockeyist.GetAngleTo(oppNetCornerX, oppNetTopCornerY)) <= CancelShootingAngleError)
                strike = true;

            if (myHockeyist.GetDistanceTo(oppNetCornerX, oppNetBottomCornerY) < DistToMyNetCornerWhenIShouldNotStrike &&
                Math.Abs(myHockeyist.GetAngleTo(oppNetCornerX, oppNetBottomCornerY)) <= CancelShootingAngleError)
                strike = true;

            return strike;
        }

       private void MoveWhenPuckIsFree(Hockeyist self, World world, Game game, Position position, Move move)
       {
           var puckMeetingPoint = GetUnitMeetingPoint(self, world.Puck, world, game);

           var puckSpeed =
               Math.Sqrt(world.Puck.SpeedX * world.Puck.SpeedX + world.Puck.SpeedY * world.Puck.SpeedY);

           var canTakePuck = puckSpeed < CriticalPuckSpeed;

           var nearestToFreePuckMyHockeyistId = GetNearestToFreePuckHockeyistId(world, game, true);
           if (self.Id == nearestToFreePuckMyHockeyistId)
           {
               move.SpeedUp = 1;
               move.Turn =
                   self.GetAngleTo(
                       self.GetDistanceTo(world.Puck) < DistWhenNeadTurn
                           ? world.Puck.X
                           : puckMeetingPoint.X,
                       self.GetDistanceTo(world.Puck) < DistWhenNeadTurn
                           ? world.Puck.Y
                           : puckMeetingPoint.Y);

               var canStrike = StrikeNearOppNet(self, world);

               move.Action = canStrike || !canTakePuck ? ActionType.Strike : ActionType.TakePuck;
           }
           else
           {
               switch (position)
               {
                   case Position.Forward:

                       if (self.GetDistanceTo(world.Puck) <= game.StickLength &&
                           Math.Abs(self.GetAngleTo(world.Puck)) < 0.5D*game.StickSector)
                       {
                           move.Action = ActionType.TakePuck;
                       }
                       else
                       {
                           var nearestToFreePuckOpponentHockeyistId = GetNearestToFreePuckHockeyistId(world, game, false);
                           AttackOpponentHockeyist(self,
                                                   world.Hockeyists.Single(
                                                       x => x.Id == nearestToFreePuckOpponentHockeyistId),
                                                   move, game, world, ActionType.Strike);
                       }
                       break;
                   case Position.Back:
                       var defencePoint = GetDefencePoint(world);
                       if (IsInPoint(defencePoint.X, defencePoint.Y, self, world, EpsDefencePointDist))
                       {
                           var speed = Math.Sqrt(self.SpeedX * self.SpeedX + self.SpeedY * self.SpeedY);
                           var speedAngle = GetSpeedAngle(self.SpeedX, self.SpeedY);
                           move.SpeedUp = GetSpeedUpToStop(speed * (self.Angle * speedAngle > 0 ? 1 : -1), game); 
                           move.Turn = self.GetAngleTo(world.Puck);

                           if (self.GetDistanceTo(world.Puck) <= game.StickLength &&
                               Math.Abs(self.GetAngleTo(world.Puck)) < 0.5D*game.StickSector && !canTakePuck)
                               move.Action = ActionType.Strike;
                           else
                           {
                               move.Action = ActionType.TakePuck;
                           }
                       }
                       else
                       {
                           MakeShortestMoveToPoint(defencePoint.X, defencePoint.Y, self, move, game, world);
                       }
                       break;
                   case Position.HalfBack:
                       var beforeDefencePoint = GetBeforeDefencePoint(world);
                       if (IsInPoint(beforeDefencePoint.X, beforeDefencePoint.Y, self, world, EpsDefencePointDist))
                       {
                           var speed = Math.Sqrt(self.SpeedX * self.SpeedX + self.SpeedY * self.SpeedY);
                           var speedAngle = GetSpeedAngle(self.SpeedX, self.SpeedY);
                           move.SpeedUp = GetSpeedUpToStop(speed * (self.Angle * speedAngle > 0 ? 1 : -1), game); 
                           move.Turn = self.GetAngleTo(world.Puck);

                           if (self.GetDistanceTo(world.Puck) <= game.StickLength &&
                               Math.Abs(self.GetAngleTo(world.Puck)) < 0.5D*game.StickSector && !canTakePuck)
                               move.Action = ActionType.Strike;
                           else
                           {
                               move.Action = ActionType.TakePuck;
                           }
                       }
                       else
                       {
                           MakeShortestMoveToPoint(beforeDefencePoint.X, beforeDefencePoint.Y, self, move, game, world);
                       }
                       break;

               }

           }
           
       }

        private Dictionary<long, Position> GetPositions(World world)
        {
            var distancesToMyNet = new Dictionary<Hockeyist, double>();
            var myNet = GetNetCenter(world.GetMyPlayer());

           var myHockeyists = world.Hockeyists.Where(x => x.IsTeammate && x.Type != HockeyistType.Goalie);

           foreach (var myHockeyist in myHockeyists)
           {
               distancesToMyNet.Add(myHockeyist, myHockeyist.GetDistanceTo(myNet.X, myNet.Y));
           }

            Hockeyist back = null;
            var backDistance = double.MaxValue;
            Hockeyist forward = null;
            var forwardDistance = 0d;
           

            foreach (var item in distancesToMyNet)
            {
                if (item.Value < backDistance)
                {
                    back = item.Key;
                    backDistance = item.Value;
                }
                if (item.Value > forwardDistance)
                {
                    forward = item.Key;
                    forwardDistance = item.Value;
                }
            }

            var positions = new Dictionary<long, Position>()
                {
                    {back.Id, Position.Back},
                    {forward.Id, Position.Forward}
                };

            foreach (var myHockeyist in myHockeyists.Where(x => x.Id != back.Id && x.Id != forward.Id))
            {
                positions.Add(myHockeyist.Id, Position.HalfBack);
            }

            return positions;
        }

        private bool OppenentHockeyistsCanReachMeMovingForward(Hockeyist myHockeyist, double time, double mySpeedUp, World world, Game game)
        {
            var angleToXAxis = myHockeyist.GetAngleTo(world.Width, myHockeyist.Y);
            var myNewX = myHockeyist.X + myHockeyist.SpeedX*time + mySpeedUp*Math.Cos(angleToXAxis)*time*time/2;
            var myNewY = myHockeyist.Y + myHockeyist.SpeedY*time + mySpeedUp*Math.Sin(angleToXAxis)*time*time/2;

            var puckX = world.Puck.X + myHockeyist.SpeedX * time + mySpeedUp * Math.Cos(angleToXAxis) * time * time / 2;
            var puckY = world.Puck.Y + myHockeyist.SpeedY * time + mySpeedUp * Math.Sin(angleToXAxis) * time * time / 2;

            foreach (
                var oppHockeysit in
                    world.Hockeyists.Where(
                        x => !x.IsTeammate && x.Type != HockeyistType.Goalie && x.RemainingCooldownTicks <= time))
            {
                var timeToMyNewPoint = GetTimeToPoint(oppHockeysit, myNewX, myNewY, world, game, true, true);
                var timeToPuck = GetTimeToPoint(oppHockeysit, puckX, puckY, world, game, true, true);
                if (timeToMyNewPoint - DeltaTime <= time || timeToPuck - DeltaTime <= time)
                    return true;
            }
            return false;
        }

        private Point GetNetCenter (Player player)
        {
            return new Point(player.NetFront,
                             (player.NetTop + player.NetBottom) / 2);
        }

       
        private void AttackOpponentHockeyist(Hockeyist myHockeyist, Hockeyist oppHockeyist, Move move, Game game, World world, ActionType puckActionType)
        {
            move.SpeedUp = 1d;
            var puckIsMine = world.Puck.OwnerPlayerId == world.GetMyPlayer().Id;

            var oppHockeyistMeetingPoint = GetUnitMeetingPoint(myHockeyist, oppHockeyist, world, game);

            if (world.Puck.OwnerHockeyistId == oppHockeyist.Id &&
                myHockeyist.GetDistanceTo(world.Puck) <= game.StickLength && Math.Abs(myHockeyist.GetAngleTo(world.Puck)) < 0.5D * game.StickSector)
            {
                var notStrikeNearMyNet = NotStrikeNearMyNet(myHockeyist, world);
                var strikeNearOppNet = StrikeNearOppNet(myHockeyist, world);

                if (notStrikeNearMyNet)
                {
                    move.Action = ActionType.TakePuck;
                }
                else if (strikeNearOppNet)
                {
                    move.Action = ActionType.Strike;
                }
                else
                {
                    move.Action = puckActionType;
                }
            }

            else
            {
                Unit unitToTurnTo = world.Puck.OwnerHockeyistId == oppHockeyist.Id ? (Unit) world.Puck : (Unit) oppHockeyist;

                move.Turn =
                    myHockeyist.GetAngleTo(
                        myHockeyist.GetDistanceTo(world.Puck) < DistWhenNeadTurn
                            ? unitToTurnTo.X
                            : oppHockeyistMeetingPoint.X,
                        myHockeyist.GetDistanceTo(world.Puck) < DistWhenNeadTurn
                            ? unitToTurnTo.Y
                            : oppHockeyistMeetingPoint.Y);

                var canStrike = false;

                foreach (
                    var justOppHockeyist in world.Hockeyists.Where(x => !x.IsTeammate && x.Type != HockeyistType.Goalie))
                {
                    if (myHockeyist.GetDistanceTo(justOppHockeyist) <= game.StickLength &&
                        Math.Abs(myHockeyist.GetAngleTo(oppHockeyist)) < 0.5D*game.StickSector &&
                        (!puckIsMine || myHockeyist.GetDistanceTo(world.Puck) > game.StickLength))
                    {
                        canStrike = true;
                    }
                }

                if (canStrike)
                {
                    move.Action = ActionType.Strike;
                }
            }
        }

        private bool HasOpponentHockeyistsOnLine(Hockeyist myHockeyist, double xDest, double yDest, World world, Game game)
        {
            foreach (
                var oppHockeyist in
                    world.Hockeyists.Where(x => !x.IsTeammate && x.Type != HockeyistType.Goalie))
            {
                var dist = GetDistanceFromPointToLine(
                    oppHockeyist.X,
                    oppHockeyist.Y,
                    myHockeyist.X,
                    myHockeyist.Y,
                    xDest,
                    yDest);
                if (dist <= game.StickLength &&
                    oppHockeyist.GetDistanceTo(xDest, yDest) < myHockeyist.GetDistanceTo(xDest, yDest))
                    return true;
            }
            return false;
        }

        private void TryToGetPuckOrStrikeOpponentIfPossible(Hockeyist myHockeyist, Hockeyist oppHockeyist, Game game, Move move, World world)
        {
            if (oppHockeyist != null && myHockeyist.GetDistanceTo(oppHockeyist) <= game.StickLength &&
                Math.Abs(myHockeyist.GetAngleTo(oppHockeyist)) < 0.5D * game.StickSector)
            {
                move.Action = ActionType.Strike;
            }
            else
            {
                var puckMeetingPoint = GetUnitMeetingPoint(myHockeyist, world.Puck, world, game);

                move.SpeedUp = 1;
                move.Turn =
                    myHockeyist.GetAngleTo(
                        myHockeyist.GetDistanceTo(world.Puck) < DistWhenNeadTurn ? world.Puck.X : puckMeetingPoint.X,
                        myHockeyist.GetDistanceTo(world.Puck) < DistWhenNeadTurn ? world.Puck.Y : puckMeetingPoint.Y);
                move.Action = ActionType.TakePuck;
            }
        }

        private long GetNearestToFreePuckHockeyistId(World world, Game game, bool isMine)
        {
            long nearestHocleyistId = -1;
            var minTime = double.MaxValue;

            foreach (var hockeyist in world.Hockeyists.Where(x => x.IsTeammate == isMine && x.Type != HockeyistType.Goalie))
            {

                var time = GetTimeToMeetUnit(hockeyist, world.Puck, world, game, true);
                if (time < minTime)
                {
                    minTime = time;
                    nearestHocleyistId = hockeyist.Id;
                }
            }
            return nearestHocleyistId;
        }

        private double GetTimeToMeetUnit(Hockeyist hockeyist, Unit unit, World world, Game game, bool considerStickLength)
        {
            var unitMeetinPoint = GetUnitMeetingPoint(hockeyist, unit, world, game);
            var time = GetTimeToPoint(hockeyist, unitMeetinPoint.X, unitMeetinPoint.Y, world, game, considerStickLength, true);
            return time;
        }

        private double GetTimeToStaticPuck(Hockeyist hockeyist, Point puckMeetingPoint, Game game)
        {
            var speed = Math.Sqrt(hockeyist.SpeedX * hockeyist.SpeedX + hockeyist.SpeedY * hockeyist.SpeedY);
            var speedProection = speed * Math.Cos(hockeyist.GetAngleTo(puckMeetingPoint.Y, puckMeetingPoint.Y));

            var time = -speedProection +
                       Math.Sqrt(
                           speedProection * speedProection +
                           2 * game.HockeyistSpeedUpFactor * hockeyist.GetDistanceTo(puckMeetingPoint.X, puckMeetingPoint.Y));

            return time;
        }
        

        private double GetDistanceFromPointToLine(double x, double y, double x0, double y0, double x1, double y1)
        {
            return
                Math.Abs(
                    ((y0 - y1) * x + (x1 - x0) * y + (x0 * y1 - x1 * y0)) /
                    Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)));
        }

        private Point GetUnitMeetingPoint(Hockeyist hockeyist, Unit unit, World world, Game game)
        {
            var time = 0d;
            var speedUp = unit.Id == world.Puck.Id ? 0 : game.HockeyistSpeedUpFactor;
            var isHockeyistWithPuck = unit.Id == world.Puck.OwnerHockeyistId;

            var newX = unit.X;
            var newY = unit.Y;

            if (isHockeyistWithPuck)
            {
                newX += (unit.Radius + world.Puck.Radius)*Math.Cos(unit.Angle);
                newY += (unit.Radius + world.Puck.Radius)*Math.Sin(unit.Angle);
            }

            var timeToNewPoint = GetTimeToPoint(hockeyist, newX, newY, world, game, true, true);

            while (timeToNewPoint > time)
            {
                newX = unit.X + unit.SpeedX * time + speedUp * Math.Cos(unit.Angle) * time * time / 2;
                newY = unit.Y + unit.SpeedY * time + speedUp * Math.Sin(unit.Angle) * time * time / 2;

                if (isHockeyistWithPuck)
                {
                    newX += (unit.Radius + world.Puck.Radius) * Math.Cos(unit.Angle);
                    newY += (unit.Radius + world.Puck.Radius) * Math.Sin(unit.Angle);
                }

                if (newX < LeftX || newX > RightX || newY < TopY || newY > BottomY )
                    break;

                timeToNewPoint = GetTimeToPoint(hockeyist, newX, newY, world, game, true, true);
                time++;
            }
            return new Point(newX, newY);
            
        }

        private Point GetShootingPoint(World world, Hockeyist hockeyist)
        {
            var shootingPointX = world.GetMyPlayer().NetFront < world.GetOpponentPlayer().NetFront
                                     ? world.GetOpponentPlayer().NetFront - ShootingPointXShift
                                     : world.GetOpponentPlayer().NetFront + ShootingPointXShift;


            var shootingPointY = hockeyist.Y < CenterY
                                     ? world.GetOpponentPlayer().NetTop
                                     : world.GetOpponentPlayer().NetBottom;

            return new Point(shootingPointX, shootingPointY);
        }

        

        private Polygon GetShootingSquare(Point shootingPoint, World world)
        {

            var shootingPointX2 = world.GetMyPlayer().NetFront < world.GetOpponentPlayer().NetFront
                                      ? shootingPoint.X - ShootingSquareSide
                                      : shootingPoint.X + ShootingSquareSide;


            var shootingPointY2 = shootingPoint.Y < CenterY
                                      ? shootingPoint.Y - ShootingSquareSide
                                      : shootingPoint.Y + ShootingSquareSide;

            var left = shootingPoint.X < shootingPointX2 ? shootingPoint.X : shootingPointX2;
            var right = shootingPoint.X < shootingPointX2 ? shootingPointX2 : shootingPoint.X;
            var bottom = shootingPoint.Y < shootingPointY2 ? shootingPointY2 : shootingPoint.Y;
            var top = shootingPoint.Y < shootingPointY2 ? shootingPoint.Y : shootingPointY2;

            return new Polygon(new List<Edge>
                {
                    new Edge(new Point(left, bottom), new Point(left, top)),
                    new Edge(new Point(left, top), new Point(right, top)),
                    new Edge(new Point(right, top), new Point(right, bottom)),
                    new Edge(new Point(right, bottom), new Point(left, bottom)),
                });

        }
      

        private void MakeShortestMoveToPoint(double x, double y, Hockeyist hockeyist, Move move, Game game, World world)
        {
            var puckSpeed = Math.Sqrt(world.Puck.SpeedX * world.Puck.SpeedX + world.Puck.SpeedY * world.Puck.SpeedY);

            var canTakePuck = puckSpeed < CriticalPuckSpeed;

            if (hockeyist.GetDistanceTo(world.Puck) <= game.StickLength &&
                Math.Abs(hockeyist.GetAngleTo(world.Puck)) < 0.5D*game.StickSector && world.Puck.OwnerPlayerId != world.GetMyPlayer().Id)
            {
                move.Action = canTakePuck ? ActionType.TakePuck : ActionType.Strike;
            }
            else
            {
                var canStrikeOppHockeyist = false;
                foreach (var oppHockeyist in world.Hockeyists.Where(t => !t.IsTeammate && t.Type != HockeyistType.Goalie
                    && hockeyist.State != HockeyistState.KnockedDown
                    && hockeyist.State != HockeyistState.Resting))
                {
                    if (hockeyist.GetDistanceTo(oppHockeyist) <= game.StickLength &&
                        Math.Abs(hockeyist.GetAngleTo(oppHockeyist)) < 0.5D*game.StickSector)
                    {
                        canStrikeOppHockeyist = true;
                        break;
                    }
                }
                if (canStrikeOppHockeyist)
                    move.Action = ActionType.Strike;
            }
               

            double resultAngle;
            bool goForward;
            var angleToPoint = hockeyist.GetAngleTo(x, y);
            //var angleToPuck = hockeyist.GetAngleTo(world.Puck.X, world.Puck.Y);
            if (Math.Abs(angleToPoint) < Math.PI / 2)
            {
                resultAngle = angleToPoint;
                goForward = true;
            }
            else
            {
                resultAngle = angleToPoint > 0 ? angleToPoint - Math.PI : angleToPoint + Math.PI;
                goForward = false;
            }
            move.Turn = resultAngle;

            var speed = Math.Sqrt(hockeyist.SpeedX * hockeyist.SpeedX + hockeyist.SpeedY * hockeyist.SpeedY);
            var canIncreaseSpeed = CanIncreaseSpeed(
                speed,
                hockeyist.X, hockeyist.Y, x, y, goForward ? game.HockeyistSpeedUpFactor : game.HockeyistSpeedDownFactor,
                goForward ? -game.HockeyistSpeedDownFactor : -game.HockeyistSpeedUpFactor,
                hockeyist.GetAngleTo(x, y));

            if (canIncreaseSpeed)
            {
                if (goForward)
                    move.SpeedUp = 1d;
                else
                    move.SpeedUp = -1d;
            }
            else
            {
                if (goForward)
                    move.SpeedUp = -1d;
                else
                    move.SpeedUp = 1d;
            }
        }


        private bool CanIncreaseSpeed(double speed, double currX, double currY, double destX, double destY, double maxSpeedUpFactor, double maxSpeedDown, double angle)
        {
            var newX = currX + speed*Math.Cos(angle)*1 + maxSpeedUpFactor*Math.Cos(angle)*1*1/2;
            var newY = currY + speed*Math.Sin(angle)*1 + maxSpeedUpFactor*Math.Sin(angle)*1*1/2;
            var distance = Math.Sqrt((newX - destX)*(newX - destX) + (newY - destY)*(newY - destY));
            return speed * Math.Cos(angle) * speed * Math.Cos(angle) + 2 * maxSpeedDown * Math.Cos(angle) * distance * Math.Cos(angle) < 0;
        }

        private bool IsInPoint(double x, double y, Hockeyist hockeyist, World world, double eps)
        {
            return Math.Abs(hockeyist.X - x) < eps &&
                   Math.Abs(hockeyist.Y - y) < eps;
        }

        private Point GetDefencePoint(World world)
        {
            var myNetIsLeft = world.GetMyPlayer().NetFront < world.Width / 2;
            var x = myNetIsLeft
                        ? world.GetMyPlayer().NetFront + 4 * world.Hockeyists.First().Radius
                        : world.GetMyPlayer().NetFront - 4 * world.Hockeyists.First().Radius;
            var y = CenterY;
            return new Point(x, y);
        }

        private Point GetBeforeDefencePoint(World world)
        {
            var myNetIsLeft = world.GetMyPlayer().NetFront < world.Width / 2;
            var x = myNetIsLeft
                        ? world.GetMyPlayer().NetFront + 10 * world.Hockeyists.First().Radius
                        : world.GetMyPlayer().NetFront - 10 * world.Hockeyists.First().Radius;
            var y = CenterY;
            return new Point(x, y);
        }

        private static Hockeyist GetNearestToPuckOpponent(World world)
        {
            Hockeyist nearestOpponent = null;
            double minDist = double.MaxValue;

            foreach (var hockeyist in world.Hockeyists)
            {
                if (hockeyist.IsTeammate || hockeyist.Type == HockeyistType.Goalie
                    || hockeyist.State == HockeyistState.KnockedDown
                    || hockeyist.State == HockeyistState.Resting)
                {
                    continue;
                }

                var dist = hockeyist.GetDistanceTo(world.Puck);

                if (dist < minDist)
                {
                    nearestOpponent = hockeyist;
                    minDist = dist;
                }
            }

            return nearestOpponent;
        }

        private static Hockeyist GetNearestBeforeOpponent(Hockeyist myHockeyist, World world)
        {
            Hockeyist nearestOpponent = null;
            double nearestOpponentRange = 0.0D;

            foreach (var hockeyist in world.Hockeyists)
            {
                if (hockeyist.IsTeammate || hockeyist.Type == HockeyistType.Goalie
                    || hockeyist.State == HockeyistState.KnockedDown
                    || hockeyist.State == HockeyistState.Resting)
                {
                    continue;
                }

                var opponentNetCenterX = (world.GetOpponentPlayer().NetLeft + world.GetOpponentPlayer().NetRight) / 2;
                var opponentNetCenterY = (world.GetOpponentPlayer().NetTop + world.GetOpponentPlayer().NetBottom) / 2;

                if (myHockeyist.GetDistanceTo(opponentNetCenterX, opponentNetCenterY) <
                    hockeyist.GetDistanceTo(opponentNetCenterX, opponentNetCenterY))
                    continue;

                double opponentRange =
                    Math.Sqrt((myHockeyist.X - hockeyist.X) * (myHockeyist.X - hockeyist.X) +
                              (myHockeyist.Y - hockeyist.Y) * (myHockeyist.Y - hockeyist.Y));

                if (nearestOpponent == null || opponentRange < nearestOpponentRange)
                {
                    nearestOpponent = hockeyist;
                    nearestOpponentRange = opponentRange;
                }
            }

            return nearestOpponent;
        }

        private int GetTimeToPoint(Hockeyist hockeyist, double xDest, double yDest, World world, Game game, bool considerStickLength, bool goForward)
        {
            var time = 0d;
            var startAngle = hockeyist.GetAngleTo(xDest, yDest);
            double angle;
            if (goForward)
            {
                angle = startAngle;
            }
            else
            {
                angle = startAngle > 0 ? startAngle - Math.PI : startAngle + Math.PI;
            }
            var x = hockeyist.X;
            var y = hockeyist.Y;
            var speedX = hockeyist.SpeedX;
            var speedY = hockeyist.SpeedY;
            var deltaAngle = Math.PI / 60.0 * hockeyist.Agility * (angle > 0 ? -1 : 1) / 100.0;
            var angleToXAxis = hockeyist.GetAngleTo(world.Width, hockeyist.Y);
            var speedUp = goForward ? game.HockeyistSpeedUpFactor : game.HockeyistSpeedDownFactor;

            while (Math.Abs(angle) > EpsAngle)
            {
                if (considerStickLength)
                {
                    if (hockeyist.GetDistanceTo(xDest, yDest) <= game.StickLength &&
                        Math.Abs(hockeyist.GetAngleTo(xDest, yDest)) < 0.5D * game.StickSector)
                    {
                        return Convert.ToInt32(time);
                    }
                }

                var realDeltaAngle = Math.Abs(angle) > Math.Abs(deltaAngle) ? deltaAngle : -angle;

                angle += realDeltaAngle;

                angleToXAxis += realDeltaAngle;



                x = x + speedX * 1 + speedUp * Math.Cos(angleToXAxis) * 1 * 1 / 2;
                y = y + speedY * 1 + speedUp * Math.Sin(angleToXAxis) * 1 * 1 / 2;

                speedX = speedX + speedUp * Math.Cos(angleToXAxis) * 1;
                speedY = speedY + speedUp * Math.Sin(angleToXAxis) * 1;


                time++;
            }



            var speed = Math.Sqrt(speedX * speedX + speedY * speedY);
            var dist = Math.Sqrt((x - xDest) * (x - xDest) + (y - yDest) * (y - yDest));
            if (considerStickLength) dist -= game.StickLength;

            if (dist < 0) dist = 0;

            time += (-speed + Math.Sqrt(speed * speed + 2 * speedUp * dist)) / speedUp;

            return Convert.ToInt32(Math.Truncate(time) == time ? Math.Truncate(time) : Math.Truncate(time) + 1);

        }

        
        private Point GetEdgeAndPolygonIntersectionPoint(Edge e, Polygon p)
        {
            foreach (var polygonEdge in p.Edges)
            {
                var intercestionPoint = GetEdgesIntersetionPoint(e, polygonEdge, true);
                if (intercestionPoint != null)
                    return intercestionPoint;
            }

            throw new Exception("Не удалось найти пересечение полигона и отрезка");
        }

        private Point GetEdgesIntersetionPoint(Edge e1, Edge e2, bool onlyInsideEdges)
        {
            var ua = ((e2.Dest.X - e2.Org.X) * (e1.Org.Y - e2.Org.Y) -
                          (e2.Dest.Y - e2.Org.Y) * (e1.Org.X - e2.Org.X)) /
                         ((e2.Dest.Y - e2.Org.Y) * (e1.Dest.X - e1.Org.X) -
                          (e2.Dest.X - e2.Org.X) * (e1.Dest.Y - e1.Org.Y));

            var ub = ((e1.Dest.X - e1.Org.X) * (e1.Org.Y - e2.Org.Y) -
                      (e1.Dest.Y - e1.Org.Y) * (e1.Org.X - e2.Org.X)) /
                     ((e2.Dest.Y - e2.Org.Y) * (e1.Dest.X - e1.Org.X) -
                      (e2.Dest.X - e2.Org.X) * (e1.Dest.Y - e1.Org.Y));
            
            if (onlyInsideEdges)
            {
                if (ua >= 0 && ua <= 1 && ub >= 0 && ub <= 1)
                {
                    return new Point(e1.Org.X + ua * (e1.Dest.X - e1.Org.X), e1.Org.Y + ua * (e1.Dest.Y - e1.Org.Y));
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return new Point(e1.Org.X + ua * (e1.Dest.X - e1.Org.X), e1.Org.Y + ua * (e1.Dest.Y - e1.Org.Y));
            }
        }

        private bool CanInterceptPuck(double startX, double startY, double startSpeed, double angle, World world, Game game)
        {
            foreach (var oppHockeysit in world.Hockeyists.Where(x => !x.IsTeammate && x.Type != HockeyistType.Goalie))
            {
                var time = 0d;
                var speed = startSpeed;

                while (true)
                {
                    var newX = startX + speed * Math.Cos(angle) * time;
                    var newY = startY + speed * Math.Sin(angle) * time;

                    if (newX < LeftX || newX > RightX || newY < TopY || newY > BottomY)
                        break;

                    var meetingTime = GetTimeToPoint(oppHockeysit, newX, newY, world, game, true, true);
                    if (meetingTime - DeltaTime <= time)
                        return true;

                    time++;

                    speed -= PuckSpeedLoss;

                }
            }

            return false;
        }

        private double GetStrikePower(int swingTicks, Game game)
        {
            return 0.75 + 0.25*(swingTicks * 1.0/game.MaxEffectiveSwingTicks);
        }

        private double GetSpeedAngle(double speedX, double speedY)
        {
            if (speedY == 0)
                return speedX > 0 ? 0 : Math.PI;
            var angle = Math.Atan(Math.Abs(speedY/speedX));
            if (speedX >= 0 && speedY > 0)
                return angle;
            else if (speedX < 0 && speedY > 0)
                return Math.PI - angle;
            else if (speedX < 0 && speedY < 0)
                return angle - Math.PI;
            else
                return -angle;
        }

        private int GetSwingTickToStrikeFromPoint(Hockeyist self, World world, Game game)
        {

            var puckLine = new Edge(new Point(world.Puck.X, world.Puck.Y),
                                    new Point(world.GetOpponentPlayer().NetFront,
                                              self.Y < CenterY
                                                  ? world.GetOpponentPlayer().NetBottom
                                                  : world.GetOpponentPlayer().NetTop));

            var netLine = new Edge(new Point(world.GetOpponentPlayer().NetFront, world.GetOpponentPlayer().NetTop),
                                   new Point(world.GetOpponentPlayer().NetFront, world.GetOpponentPlayer().NetBottom));
            var netPoint = GetEdgesIntersetionPoint(puckLine, netLine, false);
            var distFromPuckToNetPoint =
                Math.Sqrt((world.Puck.X - netPoint.X)*(world.Puck.X - netPoint.X) +
                          (world.Puck.Y - netPoint.Y)*(world.Puck.Y - netPoint.Y));
           

            var oppGk = world.Hockeyists.SingleOrDefault(x => !x.IsTeammate && x.Type == HockeyistType.Goalie);
            if (oppGk == null) return 0;

            double distFromGkYToNetPoint;
            if (world.Puck.Y <= world.GetOpponentPlayer().NetBottom - oppGk.Radius &&
                world.Puck.Y >= world.GetOpponentPlayer().NetTop + oppGk.Radius)
            {
                distFromGkYToNetPoint = distFromPuckToNetPoint;
            }
            else
            {
                var gkLineIntercectionPoint = GetEdgesIntersetionPoint(puckLine,
                                                                       new Edge(new Point(oppGk.X, oppGk.Y),
                                                                                new Point(world.Width/2, oppGk.Y)),
                                                                       false);
                distFromGkYToNetPoint = distFromPuckToNetPoint -
                                   world.Puck.GetDistanceTo(gkLineIntercectionPoint.X, gkLineIntercectionPoint.Y);
            }
            var gkMovementCoeff = self.Y < CenterY ? 1 : -1;

            for (int i = 0; i < game.MaxEffectiveSwingTicks; ++i)
            {
                var startPuckSpeed = GetPuckAfterStrikeSpeed(self, game, i);
                var puckSpeed = startPuckSpeed;
               
                var dist = 0d;
                while (dist < distFromPuckToNetPoint - distFromGkYToNetPoint)
                {
                    dist += puckSpeed;
                    puckSpeed -= PuckSpeedLoss;
                }

                var puckTimeToGkY = 0;

                dist = 0;
                while (dist < distFromGkYToNetPoint)
                {
                    dist += puckSpeed;
                    puckSpeed -= PuckSpeedLoss;
                    puckTimeToGkY++;

                }

                //var puckTimeToGkY = distFromGkYToNetPoint / puckSpeed;

                var gkTime = 0d;
                var canIntercept = false;

                while (gkTime - 3 < puckTimeToGkY)
                {
                    var gkPos = new Point(oppGk.X, oppGk.Y + gkTime * game.GoalieMaxSpeed * gkMovementCoeff);
                    var intercectCont = LineCircleIntersection(gkPos.X, gkPos.Y, oppGk.Radius, puckLine.Org.X,
                                                               puckLine.Org.Y, puckLine.Dest.X, puckLine.Dest.Y);

                    if (intercectCont > 0)
                    {
                        canIntercept = true;
                        break;
                    }

                    gkTime++;
                }
                if (!canIntercept)
                    return i;
            }

            return -1;

        }

        

        private double GetPuckAfterStrikeSpeed(Hockeyist self, Game game, int swingTicks)
        {
            //var selfSpeed = Math.Sqrt(self.SpeedX * self.SpeedX + self.SpeedY * self.SpeedY);
            //var speedAngle = GetSpeedAngle(self.SpeedX, self.SpeedY);
            var puckSpeed = 20.0*GetStrikePower(swingTicks, game)*self.Strength/100.0;
                            //selfSpeed * Math.Cos(self.Angle - speedAngle);

            return puckSpeed;

        }


        private bool EqualDoubles(double n1, double n2, double precision)
        {
            return (Math.Abs(n1 - n2) <= precision);
        }


        // пересечение линии, проходящей через отрезок, с окружностью;
        // возвращает число точек пересечения: 0 или 1 или 2;
        private int LineCircleIntersection(double x0, double y0, double r, // центр и рдиус окружности
                                           double x1, double y1, // точки
                                           double x2, double y2 //    отрезка
            )
        {
            double q = x0*x0 + y0*y0 - r*r;
            double k = -2.0*x0;
            double l = -2.0*y0;

            double z = x1*y2 - x2*y1;
            double p = y1 - y2;
            double s = x1 - x2;

            if (EqualDoubles(s, 0.0, 0.001))
            {
                s = 0.001;
            }

            double A = s*s + p*p;
            double B = s*s*k + 2.0*z*p + s*l*p;
            double C = q*s*s + z*z + s*l*z;

            double D = B*B - 4.0*A*C;

            if (D < 0.0)
            {
                return 0;
            }
            else if (D < 0.001)
            {
                return 1;
            }

            return 2;
        }

        private double GetSpeedUpToStop(double speed, Game game)
        {
            if (speed >= 0)
            {
                if (speed > Math.Abs(game.HockeyistSpeedDownFactor))
                {
                    return -1;
                }
                else
                {
                    return -speed / Math.Abs(game.HockeyistSpeedDownFactor);
                }
            }
            else
            {
                if (Math.Abs(speed) > Math.Abs(game.HockeyistSpeedUpFactor))
                {
                    return 1;
                }
                else
                {
                    return -speed / Math.Abs(game.HockeyistSpeedUpFactor);
                }
            }
        }

    }
}