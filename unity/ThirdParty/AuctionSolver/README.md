# Auction Solver Notices

The unmodified Windows x64 solver binaries are restored from these official NuGet packages:

- https://www.nuget.org/packages/Google.OrTools/9.15.6755
- https://www.nuget.org/packages/Google.OrTools.runtime.win-x64/9.15.6755
- https://www.nuget.org/packages/Google.Protobuf/3.33.1

`tools/Install-AuctionSolver.ps1` verifies SHA-256 package hashes. The notices in
this folder accompany the Windows build. Source and dependency versions for
OR-Tools are available at https://github.com/google/or-tools/tree/v9.15 and
https://github.com/google/or-tools/blob/v9.15/cmake/dependencies/CMakeLists.txt.
The upstream binaries may contain solvers unused by this game's CP-SAT adapter.

Upstream notice sources:

| Component | Source version / license file |
| --- | --- |
| OR-Tools | https://github.com/google/or-tools/blob/v9.15/LICENSE |
| Protobuf | https://github.com/protocolbuffers/protobuf/blob/v33.1/LICENSE |
| Abseil | https://github.com/abseil/abseil-cpp/blob/20250814.1/LICENSE |
| RE2 | https://github.com/google/re2/blob/2025-08-12/LICENSE |
| HiGHS | https://github.com/ERGO-Code/HiGHS/blob/v1.12.0/LICENSE.txt |
| SCIP | https://github.com/scipopt/scip/blob/v10.0.0/LICENSE |
| SoPlex | https://github.com/scipopt/soplex/blob/v8.0.0/LICENSE |
| zlib | https://github.com/madler/zlib/blob/v1.3.1/LICENSE |
| utf8-range | https://github.com/protocolbuffers/protobuf/blob/v33.1/third_party/utf8_range/LICENSE |
| Boost | https://github.com/boostorg/boost/blob/boost-1.87.0/LICENSE_1_0.txt |
| bzip2 | https://github.com/libarchive/bzip2/blob/bzip2-1.0.8/LICENSE |
| Eigen | https://gitlab.com/libeigen/eigen/-/blob/3.4.0/COPYING.MPL2 |
| CoinUtils | https://github.com/Mizux/CoinUtils/blob/cmake/2.11.12/LICENSE |
| Osi | https://github.com/Mizux/Osi/blob/cmake/0.108.11/LICENSE |
| Clp | https://github.com/Mizux/Clp/blob/cmake/1.17.10/LICENSE |
| Cgl | https://github.com/Mizux/Cgl/blob/cmake/0.60.9/LICENSE |
| Cbc | https://github.com/Mizux/Cbc/blob/cmake/2.10.12/LICENSE |
