// ============================================================
// NEW FILE: Lock/Models/GiftDefinition.cs
// ============================================================
using System.Collections.Generic;
namespace Lock.Models
{
    public class GiftDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public string IconColor { get; set; } = "#FF3B6F";
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AnimationColor { get; set; } = "#FF3B6F";
        public string BubbleGradientStart { get; set; } = "#FF3B6F";
        public string BubbleGradientEnd { get; set; } = "#FF6B9D";
        public int ParticleCount { get; set; } = 20;

        public static List<GiftDefinition> All => new()
        {
            new GiftDefinition
            {
                Id = "diamond",
                IconPath = "M481.5-137 176-436.5 342.5-637h277l166 200.5-304 299.5Zm-249-529-75-74 18.5-18.5 74.5 74-18 18.5Zm236-52v-105H494v105h-25.5ZM728-666l-17-18 74-74 18 18-75 74ZM481.5-172.5l257.5-253H222.5l259 253ZM354-611.5 221-451h519.5l-133-160.5H354Z",
                IconColor = "#B9F0FF",
                Name = "Diamond",
                Description = "Rare & precious",
                AnimationColor = "#00D4FF",
                BubbleGradientStart = "#00B5D8",
                BubbleGradientEnd = "#0077FF",
                ParticleCount = 30
            },
            new GiftDefinition
            {
                Id = "crown",
                IconPath = "M247-217v-25.5h466.5v25.5H247Zm-3.5-106.5-59-297q-2.5 1-5.18 1h-4.82q-15.75 0-26.62-11.06Q137-641.62 137-657.41q0-16.42 10.88-28.01Q158.75-697 174.56-697t27.62 11.52Q214-673.96 214-657.5q0 3.82-.25 6.66Q213.5-648 212-645l129.25 50L457.5-753.54q-8-5.2-12.5-13.19-4.5-7.99-4.5-16.77 0-15.83 11.71-27.67Q463.92-823 480.19-823q16.06 0 27.69 11.78 11.62 11.77 11.62 27.52 0 9.47-4.5 17.22-4.5 7.74-12.5 12.94L618.75-595 748-645q-.8-2.65-1.4-5.82-.6-3.18-.6-6.68 0-16.46 10.56-27.98 10.57-11.52 27-11.52 16.02 0 27.73 11.55Q823-673.91 823-657.41q0 15.51-11.79 26.71-11.78 11.2-27.9 11.2-1.66 0-3.74-.5-2.07-.5-4.41-.5l-58.99 297H243.5Zm22-25.5h429l56-269L610-563.5l-130-177-130 177L209.5-618l56 269Zm214.5 0Z",
                IconColor = "#FFD700",
                Name = "Crown",
                Description = "You rule!",
                AnimationColor = "#FFD700",
                BubbleGradientStart = "#DAA520",
                BubbleGradientEnd = "#FFD700",
                ParticleCount = 28
            },
            new GiftDefinition
            {
                Id = "rose",
                IconPath = "M460-168.5q-9.5-27.24-23.75-50.19Q422-241.65 403-260.82q-19-19.18-41.75-33.43T311.5-318q2.5 30 14.5 56t32 46q20 20 45.75 32.25T460-168.5Zm39.5 0q30-2 56.03-14.25T601.78-215q20.22-20 32.47-46.25T648.5-318q-26.76 9.5-49.56 23.75-22.79 14.25-41.86 33.5Q538-241.5 523.37-218.5q-14.63 23-23.87 50Zm88-364.25q44-43.75 44-107.25v-81l-71 60L480-770l-80 109-71-60v81q0 63.5 43.75 107.25T480-489q63.5 0 107.5-43.75ZM472-143.5q-77.83 0-132.17-54.33Q285.5-252.17 285.5-330v-21q61 13.5 110.25 52t71.75 91v-255q-69-5-116.5-56t-47.5-121v-136.5l93 80L480-809l84 112.5 93-80V-640q0 70-47.75 121T493-463v253.5q22-51.5 71-89.75T673-351v21q0 77.83-54.29 132.17Q564.42-143.5 487-143.5h-15Zm8-486Zm94 386Zm-188 0Z",
                IconColor = "#FF3B6F",
                Name = "Rose",
                Description = "With love",
                AnimationColor = "#FF3B6F",
                BubbleGradientStart = "#FF3B6F",
                BubbleGradientEnd = "#FF6B9D",
                ParticleCount = 20
            },
            new GiftDefinition
            {
                Id = "giftbox",
                IconPath = "M177-137v-427h-40v-150.5h203.5q-14.5-11-20-26.81-5.5-15.8-5.5-33.69 0-35.5 25.08-60.75Q365.17-861 401-861q22.82 0 41.66 11.5Q461.5-838 474.5-820q12.5-17.5 31.25-29.25T547.01-861q35.6 0 60.8 25.25Q633-810.5 633-775q0 17.5-5.5 33.75t-20 26.75H823V-564h-40v427H177Zm327.5-680.89Q487-800.29 487-775t17.25 42.89Q521.5-714.5 547-714.5q25.29 0 42.89-17.61 17.61-17.6 17.61-42.89t-17.75-42.89Q572-835.5 547-835.5t-42.5 17.61ZM340.5-775q0 25.29 17.5 42.89 17.5 17.61 42.75 17.61t43-17.61q17.75-17.6 17.75-42.89t-17.61-42.89Q426.29-835.5 401-835.5q-26 0-43.25 17.61-17.25 17.6-17.25 42.89Zm-178 86v99.5h305V-689h-305Zm305 526.5V-564h-265v401.5h265Zm25.5 0h264.5V-564H493v401.5Zm304.5-427V-689H493v99.5h304.5Z",
                IconColor = "#FF3B6F",
                Name = "Gift Box",
                Description = "Surprise!",
                AnimationColor = "#8B5CF6",
                BubbleGradientStart = "#7C3AED",
                BubbleGradientEnd = "#A78BFA",
                ParticleCount = 26
            },
            new GiftDefinition
            {
                Id = "rocket",
                IconPath = "m197-492 107 45.5q21-40.5 45.5-78.75t54-73.75L337-612q-8-1.5-15.5.75T308-603L197-492Zm127.5 62.5 113 113q53-25.5 101.5-59t93.5-78.5q61.5-61.5 93.75-131T766-759q-104.5 7.5-173.5 39.5T462-626q-45 45-78.5 94t-59 102.5Zm207-148q0-21.5 15.75-37t37.5-15.5q21.75 0 37.5 15.5t15.75 37q0 21.5-15.75 37.25t-37.5 15.75q-21.75 0-37.5-15.75T531.5-577.5ZM499-189l111-111q6-6 8.25-13.5T619-329l-13-66.5q-35.5 29-73.75 53.75T453.5-296L499-189Zm291.5-593.5q4.5 101-32.25 188.5T650.5-435.5l-11.25 11.25L628-413l15 78.5q3 14.5-1 28t-14 24l-138 137-60.5-143-134.5-134L152.5-484 290-621q10-10.5 23.5-15t28-1.5L422-621q5.5-6 10.5-11.25t11-11.25q70.5-71 158.25-107.25T790.5-782.5ZM221-309.5q20-20 47.5-19.25T316-308.5q20 20 20 47.75t-19.5 47.25Q297-194 257-180.75T171.5-165q3-45 16.75-85.25T221-309.5Zm18.5 19.5q-13 13-23 39.25t-13 55.25q29-3 55.25-13.5T298-232.5q12.5-12 12.25-29.25T298-291q-12.5-12-29.5-11.75t-29 12.75Z",
                IconColor = "#00B5B5",
                Name = "Rocket",
                Description = "To the moon!",
                AnimationColor = "#00B5B5",
                BubbleGradientStart = "#006994",
                BubbleGradientEnd = "#00CED1",
                ParticleCount = 22
            },
            new GiftDefinition
            {
                Id = "heart",
                IconPath = "M480-195.5 460-214q-96.13-88.18-159.07-150.59Q238-427 201.72-473.9q-36.29-46.9-50.5-84.48Q137-595.95 137-633.5q0-69.5 47.5-117t117-47.5q52.47 0 97.98 28.5Q445-741 480-685.5q35.5-55.5 80.75-84T658.5-798q69.5 0 117 47.44Q823-703.11 823-633.69q0 37.73-14.22 75.31-14.21 37.58-50.46 84.42-36.25 46.85-98.9 109.37Q596.77-302.07 500-214l-20 18.5Zm0-34.5q94.82-86.57 156.35-147.98t97.34-106.72q35.81-45.3 49.81-80.09 14-34.78 14-68.57 0-59.64-39.86-99.39t-98.89-39.75q-48.25 0-88.5 27.5t-77.75 87h-25q-38.5-60.5-78.25-87.5t-88-27q-58.03 0-98.39 39.75t-40.36 99.44q0 33.82 14.07 68.64t49.75 79.99Q262-439.5 323.5-378.25 385-317 480-230Zm0-271.5Z",
                IconColor = "#FF3B6F",
                Name = "Heart",
                Description = "Sending love",
                AnimationColor = "#FF4444",
                BubbleGradientStart = "#FF1744",
                BubbleGradientEnd = "#FF6B6B",
                ParticleCount = 24
            },
            new GiftDefinition
            {
                Id = "trophy",
                IconPath = "M356-176.5V-202h111.5v-159.62q-52-1.38-96.2-41.27-44.21-39.9-50.72-92.11-59.08-7-101.58-46.5t-42.5-96.95V-704H317v-79.5h326v79.5h140.5v65.55q0 57.45-42.5 96.95T639.71-495q-6.71 52-50.86 91.96-44.16 39.96-95.85 41.54V-202h111.5v25.5H356Zm-39-346v-156H202v40q0 47.5 33.5 80.75T317-522.5Zm259 95.98q39.5-39.52 39.5-95.98V-758h-271v235.5q0 56.46 39.68 95.98 39.67 39.52 96 39.52 56.32 0 95.82-39.52Zm67-95.98q48-2 81.5-35.25T758-638.5v-40H643v156Zm-163-50Z",
                IconColor = "#FFD700",
                Name = "Trophy",
                Description = "You're a champion!",
                AnimationColor = "#FFD700",
                BubbleGradientStart = "#DAA520",
                BubbleGradientEnd = "#FFD700",
                ParticleCount = 25
            },
            new GiftDefinition
            {
                Id = "star",
                IconPath = "m352-284.5 128-77 128 78-33.5-146 113-97.5L539-540.5l-59-138-58.5 137.68L273-528l113 98-34 145.5Zm-39 54 44.5-190.09L210-548l194-17 76-179 76.5 179L750-548 602.48-420.59l44.96 190.09-167.22-101.11L313-230.5Zm167-240Z",
                IconColor = "#FFD700",
                Name = "Star",
                Description = "You're a star!",
                AnimationColor = "#FBBF24",
                BubbleGradientStart = "#F59E0B",
                BubbleGradientEnd = "#FDE68A",
                ParticleCount = 20
            },
        };

        public static GiftDefinition? FindById(string giftId)
            => All.Find(g => g.Id == giftId);
    }
}