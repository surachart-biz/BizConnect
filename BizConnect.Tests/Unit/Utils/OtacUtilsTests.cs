using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using BizConnect.Services.Utils;

namespace BizConnect.Tests.Unit.Utils
{
    /// <summary>
    /// Comprehensive unit tests for OtacUtils focusing on code generation, validation, and normalization
    /// </summary>
    public class OtacUtilsTests
    {
        private const string ValidChars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const int ExpectedLength = 8;

        [Fact]
        public void GenerateOtacCode_ShouldReturnCorrectLength()
        {
            // Act
            var code = OtacUtils.GenerateOtacCode();

            // Assert
            Assert.Equal(ExpectedLength, code.Length);
        }

        [Fact]
        public void GenerateOtacCode_ShouldOnlyContainValidCharacters()
        {
            // Act
            var code = OtacUtils.GenerateOtacCode();

            // Assert
            Assert.All(code, c => Assert.Contains(c, ValidChars));
        }

        [Fact]
        public void GenerateOtacCode_ShouldNotContainConfusingCharacters()
        {
            // Arrange
            var confusingChars = new[] { '0', 'O', '1', 'l', 'I' };

            // Act & Assert - Generate multiple codes to increase confidence
            for (int i = 0; i < 100; i++)
            {
                var code = OtacUtils.GenerateOtacCode();
                Assert.All(confusingChars, confusing => Assert.DoesNotContain(confusing, code));
            }
        }

        [Fact]
        public void GenerateOtacCode_ShouldGenerateUniqueCodesAcrossMultipleCalls()
        {
            // Arrange
            var generatedCodes = new HashSet<string>();
            var numberOfCodes = 1000;

            // Act
            for (int i = 0; i < numberOfCodes; i++)
            {
                var code = OtacUtils.GenerateOtacCode();
                generatedCodes.Add(code);
            }

            // Assert - With a large enough character set, we should have very few collisions
            // With 32 valid characters and 8 positions, we have 32^8 = 1,099,511,627,776 possibilities
            // 1000 codes should have minimal collisions
            Assert.True(generatedCodes.Count > numberOfCodes * 0.95, 
                $"Expected at least {numberOfCodes * 0.95} unique codes, but got {generatedCodes.Count}");
        }

        [Theory]
        [InlineData("ABC23456", true)]
        [InlineData("23456789", true)]
        [InlineData("ABCDEFGH", true)]
        [InlineData("ZYXWVUTS", true)]
        public void IsValidOtacFormat_WithValidCodes_ShouldReturnTrue(string code, bool expected)
        {
            // Act
            var result = OtacUtils.IsValidOtacFormat(code);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("1234567", false)] // Too short
        [InlineData("123456789", false)] // Too long
        [InlineData("ABC1234O", false)] // Contains confusing 'O'
        [InlineData("ABC12340", false)] // Contains confusing '0'
        [InlineData("ABC1234I", false)] // Contains confusing 'I'
        [InlineData("ABC1234l", false)] // Contains confusing 'l'
        [InlineData("ABC12341", false)] // Contains confusing '1'
        [InlineData("ABC!@#$%", false)] // Contains invalid characters
        [InlineData("abc23456", true)] // Lowercase should be valid (case-insensitive)
        public void IsValidOtacFormat_WithInvalidCodes_ShouldReturnFalse(string code, bool expected)
        {
            // Act
            var result = OtacUtils.IsValidOtacFormat(code);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("abc23456", "ABC23456")]
        [InlineData("ABC23456", "ABC23456")]
        [InlineData("AbC23456", "ABC23456")]
        [InlineData("xyztuvwx", "XYZTUVWX")]
        public void NormalizeOtacCode_ShouldConvertToUppercase(string input, string expected)
        {
            // Act
            var result = OtacUtils.NormalizeOtacCode(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        public void NormalizeOtacCode_WithNullOrEmpty_ShouldReturnEmpty(string input, string expected)
        {
            // Act
            var result = OtacUtils.NormalizeOtacCode(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsValidOtacFormat_ShouldBeCaseInsensitive()
        {
            // Arrange
            var upperCase = "ABC23456";
            var lowerCase = "abc23456";
            var mixedCase = "AbC23456";

            // Act & Assert
            Assert.True(OtacUtils.IsValidOtacFormat(upperCase));
            Assert.True(OtacUtils.IsValidOtacFormat(lowerCase));
            Assert.True(OtacUtils.IsValidOtacFormat(mixedCase));
        }

        [Fact]
        public void GenerateOtacCode_ShouldUseCryptographicallySecureRandomness()
        {
            // Arrange - Generate a large number of codes to analyze distribution
            var codes = new List<string>();
            var numberOfCodes = 10000;

            // Act
            for (int i = 0; i < numberOfCodes; i++)
            {
                codes.Add(OtacUtils.GenerateOtacCode());
            }

            // Assert - Check distribution of first character (should be roughly uniform)
            var firstCharDistribution = codes
                .GroupBy(c => c[0])
                .ToDictionary(g => g.Key, g => g.Count());

            var expectedFrequencyPerChar = numberOfCodes / (double)ValidChars.Length;
            var tolerance = expectedFrequencyPerChar * 0.3; // 30% tolerance

            foreach (var validChar in ValidChars)
            {
                if (firstCharDistribution.TryGetValue(validChar, out var frequency))
                {
                    Assert.True(Math.Abs(frequency - expectedFrequencyPerChar) < tolerance,
                        $"Character '{validChar}' frequency {frequency} deviates too much from expected {expectedFrequencyPerChar}");
                }
            }
        }

        [Theory]
        [InlineData("ABC23456")]
        [InlineData("ZYXWVUTS")]
        public void NormalizeOtacCode_WithValidCode_ShouldMaintainLength(string input)
        {
            // Act
            var result = OtacUtils.NormalizeOtacCode(input);

            // Assert
            Assert.Equal(input.Length, result.Length);
        }

        [Fact]
        public void IsValidOtacFormat_WithAllValidCharacters_ShouldReturnTrue()
        {
            // Arrange - Create a code using each valid character at least once
            var codeWithAllChars = ValidChars.Substring(0, 8);

            // Act
            var result = OtacUtils.IsValidOtacFormat(codeWithAllChars);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void GenerateOtacCode_PerformanceTest_ShouldGenerateCodesQuickly()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var numberOfCodes = 1000;

            // Act
            for (int i = 0; i < numberOfCodes; i++)
            {
                OtacUtils.GenerateOtacCode();
            }

            // Assert
            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
                $"Generating {numberOfCodes} codes took {stopwatch.ElapsedMilliseconds}ms, which is too slow");
        }

        [Fact]
        public void IsValidOtacFormat_EdgeCases_ShouldHandleCorrectly()
        {
            // Test whitespace
            Assert.False(OtacUtils.IsValidOtacFormat("   ABC   "));
            Assert.False(OtacUtils.IsValidOtacFormat(" ABC23456"));
            Assert.False(OtacUtils.IsValidOtacFormat("ABC23456 "));
            
            // Test with tabs and newlines
            Assert.False(OtacUtils.IsValidOtacFormat("ABC\t2345"));
            Assert.False(OtacUtils.IsValidOtacFormat("ABC\n2345"));
            Assert.False(OtacUtils.IsValidOtacFormat("ABC\r2345"));
            
            // Test Unicode characters
            Assert.False(OtacUtils.IsValidOtacFormat("ABC234®6"));
            Assert.False(OtacUtils.IsValidOtacFormat("ABC234ñ6"));
        }

        [Theory]
        [InlineData("ABC23456XYZ", false)] // Too long
        [InlineData("ABC234", false)] // Too short
        [InlineData("ABCDEFGH", true)] // Exactly 8 characters
        [InlineData("23456789", true)] // All numbers (valid ones)
        public void IsValidOtacFormat_LengthValidation_ShouldBeExact(string code, bool expected)
        {
            // Act
            var result = OtacUtils.IsValidOtacFormat(code);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GenerateOtacCode_ThreadSafety_ShouldWorkWithConcurrentCalls()
        {
            // Arrange
            var codes = new System.Collections.Concurrent.ConcurrentBag<string>();
            var numberOfThreads = 10;
            var codesPerThread = 100;
            var tasks = new List<System.Threading.Tasks.Task>();

            // Act
            for (int i = 0; i < numberOfThreads; i++)
            {
                tasks.Add(System.Threading.Tasks.Task.Run(() =>
                {
                    for (int j = 0; j < codesPerThread; j++)
                    {
                        codes.Add(OtacUtils.GenerateOtacCode());
                    }
                }));
            }

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.Equal(numberOfThreads * codesPerThread, codes.Count);
            Assert.All(codes, code =>
            {
                Assert.Equal(8, code.Length);
                Assert.All(code, c => Assert.Contains(c, ValidChars));
            });
        }
    }
}